using Base.Core.Kafka.Interface;
using Base.Core.Model;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Base.Core.Kafka
{
    public class KafkaConsumerService(
        ILogger<KafkaConsumerService> logger,
        KafkaSetting settings,
        IKafkaProducerService? producerService) : IKafkaConsumerService
    {
        private readonly ILogger<KafkaConsumerService> _logger = logger;
        private readonly KafkaSetting _settings = settings;
        private readonly IKafkaProducerService? _producerService = producerService;

        /// <summary>
        /// 獲取Kafka配置
        /// </summary>
        public KafkaSetting Settings => _settings;

        /// <summary>
        /// 執行順序
        /// 1. Kafka Consumer poll message(單執行緒)
        /// 2. 依照 Partition 分配到對應 Channel
        /// 3. 每個 Partition 有自己的 Worker(保證順序)
        /// 4. Worker 處理完寫入 commitChannel
        /// 5. CommitWorker 統一 commit(避免 thread-unsafe) 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="processMessageFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SubscribeAsync<T>(
            Func<T, Task> processMessageFunc,
            CancellationToken cancellationToken = default)
            where T : class
        {
            var config = CreateConsumerConfig();

            // 每個partition對應一個channel(確保順序)
            var partitionChannels = new ConcurrentDictionary<TopicPartition, Channel<ConsumeResult<Ignore, string>>>();
            // 每個 partition 對應一個 worker
            var workers = new ConcurrentDictionary<TopicPartition, Task>();
            // commit queue(所有 worker 共用)
            var commitChannel = Channel.CreateUnbounded<ConsumeResult<Ignore, string>>();

            var consumer = CreateConsumer(config, partitionChannels);
            consumer.Subscribe(_settings.Topic);

            var commitWorker = RunCommitWorker(consumer, commitChannel, cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(cancellationToken);

                    if (result?.Message == null || result.IsPartitionEOF)
                        continue;

                    var tp = result.TopicPartition;

                    // 依照 Partition 取得對應的 Channel
                    // 如果不存在就建立一個新的(每個 partition 一個 channel)
                    var channel = partitionChannels.GetOrAdd(tp, _ =>
                        CreatePartitionChannel(tp, workers, commitChannel, processMessageFunc, cancellationToken));

                    await channel.Writer.WriteAsync(result, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Consumer stopping...");
            }
            finally
            {
                await CleanupResourcesAsync(partitionChannels, workers, commitChannel, commitWorker, consumer);
            }
        }

        /// <summary>
        /// 初始化Consumer設定
        /// </summary>
        /// <returns></returns>
        private ConsumerConfig CreateConsumerConfig()
        {
            return new ConsumerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                GroupId = _settings.GroupId,
                EnableAutoCommit = false,
                AutoOffsetReset = _settings.AutoOffsetReset ?? AutoOffsetReset.Latest,
                // consumer 2次poll 超過這時間會剔除Consumer Group，將partition分給別的consumer消費，做rebalance
                MaxPollIntervalMs = 300000,
                SessionTimeoutMs = 10000,
            };
        }

        /// <summary>
        /// 建立 Kafka 消費者實例以及實現 Rebalance 處理邏輯
        /// </summary>
        /// <param name="config"></param>
        /// <param name="partitionChannels"></param>
        /// <returns></returns>
        private IConsumer<Ignore, string> CreateConsumer(
            ConsumerConfig config,
            ConcurrentDictionary<TopicPartition, Channel<ConsumeResult<Ignore, string>>> partitionChannels)
        {
            return new ConsumerBuilder<Ignore, string>(config)
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    // 此段為Rebalance - Partition機制，當有新的consumer加入group or 有consumer掛掉
                    // 停止該 partition 的 channel(避免繼續處理)
                    _logger.LogWarning("Partitions revoked: {Partitions}", partitions);

                    foreach (var p in partitions)
                    {
                        var tp = p.TopicPartition;

                        // 檢查該分區是否在目前的處理清單中
                        if (partitionChannels.TryGetValue(tp, out var ch))
                        {
                            // 停止該 partition 的 channel
                            // TryComplete() 會通知對應的 PartitionWorker 停止讀取新訊息
                            // 確保不再處理不再屬於分區資料，避免重複處理或 Offset 提交衝突
                            ch.Writer.TryComplete();
                        }
                    }
                })
                .SetPartitionsAssignedHandler((c, partitions) =>
                {
                    // Rebalance - Partition分配
                    _logger.LogInformation("Partitions assigned: {Partitions}", partitions);
                })
                .Build();
        }

        /// <summary>
        /// 為 Kafka Topic Partition 建立專屬的資料傳輸通道 (Channel) 與背景工作執行緒 (Worker)
        /// 確保同分區內訊息處理的順序性，並透過 Bounded Channel 提供背壓 (Backpressure) 保護
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tp"></param>
        /// <param name="workers"></param>
        /// <param name="commitChannel"></param>
        /// <param name="processMessageFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private Channel<ConsumeResult<Ignore, string>> CreatePartitionChannel<T>(
            TopicPartition tp,
            ConcurrentDictionary<TopicPartition, Task> workers,
            Channel<ConsumeResult<Ignore, string>> commitChannel,
            Func<T, Task> processMessageFunc,
            CancellationToken cancellationToken)
            where T : class
        {
            _logger.LogInformation("Creating partition channel for {Topic} [{Partition}]", tp.Topic, tp.Partition.Value);

            /* 
            * 建立 Bounded Channel (具備容量上限的通道)
            * 1. 容量限制 (1000 筆)：防止在處理速度慢於讀取速度時，大量訊息積壓在記憶體中導致Out of Memory
            * 2. 背壓機制 (Backpressure)：當 FullMode 設為 Wait，若通道滿了，生產者(Main Loop)會暫停寫入，
            *    進而減緩從 Kafka 拉取訊息的速度，達到自我保護。
            */
            var ch = Channel.CreateBounded<ConsumeResult<Ignore, string>>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            /* 
            * 為每個 Partition 啟動一個專屬的 Worker 任務 (Task.Run)
            * 核心設計考量：
            * 1. 順序保證：Kafka 的順序性僅保證在「同一分區」內，一個 Worker 負責一個分區可確保訊息按 Offset 順序處理。
            * 2. 避免競爭：多執行緒若同時存取同一個分區的訊息，會導致 Offset 提交混亂，這裡透過隔離來避免問題。
            */
            workers[tp] = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("PartitionWorker started for {Topic} [{Partition}]", tp.Topic, tp.Partition.Value);
                    await PartitionWorker(ch, commitChannel, processMessageFunc, cancellationToken);
                    _logger.LogInformation("PartitionWorker stopped for {Topic} [{Partition}]", tp.Topic, tp.Partition.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PartitionWorker error for {Topic} [{Partition}]", tp.Topic, tp.Partition.Value);
                    throw;
                }
            }, cancellationToken);

            return ch;
        }

        /// <summary>
        /// 執行Commit Worker
        /// - 唯一操作 Kafka consumer commit 的地方
        /// - 批次處理 commit，避免頻繁 commit
        /// - 避免 muti-thread commit 造成 thread-unsafe
        /// </summary>
        /// <param name="consumer"></param>
        /// <param name="commitChannel"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private Task RunCommitWorker(
            IConsumer<Ignore, string> consumer,
            Channel<ConsumeResult<Ignore, string>> commitChannel,
            CancellationToken cancellationToken)
        {
            // 避免 multi-thread commit 造成 unthread safe
            return Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Commit worker started");
                    var batch = new List<ConsumeResult<Ignore, string>>();
                    var lastCommitTime = DateTime.UtcNow;

                    await foreach (var item in commitChannel.Reader.ReadAllAsync(cancellationToken))
                    {
                        _logger.LogDebug("Commit worker received item, offset: {Offset}", item.Offset);
                        batch.Add(item);

                        if (batch.Count >= _settings.CommitBatchSize ||
                            (DateTime.UtcNow - lastCommitTime).TotalSeconds >= 5)
                        {
                            _logger.LogInformation("Committing batch of {Count} items", batch.Count);
                            CommitByPartition(consumer, batch);
                            batch.Clear();
                            lastCommitTime = DateTime.UtcNow;
                        }
                    }

                    if (batch.Count > 0)
                    {
                        _logger.LogInformation("Final commit of {Count} items", batch.Count);
                        CommitByPartition(consumer, batch);
                    }

                    _logger.LogInformation("Commit worker stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Commit worker error");
                    throw;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 清理資源
        /// - 關閉所有 partition channels
        /// - 等待所有 worker 完成
        /// - 關閉 commit channel 並等待 commit worker 完成
        /// - 關閉 Kafka consumer
        /// </summary>
        private async Task CleanupResourcesAsync(
            ConcurrentDictionary<TopicPartition, Channel<ConsumeResult<Ignore, string>>> partitionChannels,
            ConcurrentDictionary<TopicPartition, Task> workers,
            Channel<ConsumeResult<Ignore, string>> commitChannel,
            Task commitWorker,
            IConsumer<Ignore, string> consumer)
        {
            foreach (var ch in partitionChannels.Values)
                ch.Writer.Complete();

            await Task.WhenAll(workers.Values);

            commitChannel.Writer.Complete();
            await commitWorker;

            consumer.Close();
        }


        /// <summary>
        /// 同一個 partition 必須維持順序，並有對應專屬的worker
        /// - 單線程(確保 Kafka 順序)
        /// - 負責 retry / DLQ
        /// - 不直接 commit(避免 thread-unsafe) 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="channel"></param>
        /// <param name="consumer"></param>
        /// <param name="processMessageFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task PartitionWorker<T>(
            Channel<ConsumeResult<Ignore, string>> channel,
            Channel<ConsumeResult<Ignore, string>> commitChannel,
            Func<T, Task> processMessageFunc,
            CancellationToken cancellationToken)
            where T : class
        {
            await foreach (var consumeResult in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var messageValue = consumeResult.Message.Value;

                if (string.IsNullOrEmpty(messageValue))
                    continue;

                T? messageObj;

                try
                {
                    messageObj = typeof(T) == typeof(string)
                        ? messageValue as T
                        : JsonSerializer.Deserialize<T>(messageValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deserialize failed");
                    await SendToDlq(messageValue);
                    await commitChannel.Writer.WriteAsync(consumeResult, cancellationToken);
                    continue;
                }

                if (messageObj == null)
                {
                    await SendToDlq(messageValue);
                    await commitChannel.Writer.WriteAsync(consumeResult, cancellationToken);
                    continue;
                }

                var success = false;

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        await processMessageFunc(messageObj);
                        success = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Retry {Retry}", i + 1);
                        await Task.Delay(500, cancellationToken);
                    }
                }

                if (!success)
                {
                    await SendToDlq(messageValue);
                }

                // 不管成功 or DLQ 都 commit
                await commitChannel.Writer.WriteAsync(consumeResult, cancellationToken);
            }
        }

        /// <summary>
        /// 嘗試反序列化訊息
        /// </summary>
        /// <typeparam name="T">訊息類型</typeparam>
        /// <param name="messageValue">訊息內容</param>
        /// <param name="messageObj">反序列化後的物件</param>
        /// <returns>是否成功反序列化</returns>
        private bool TryDeserializeMessage<T>(string messageValue, out T? messageObj)
            where T : class
        {
            messageObj = null;

            try
            {
                messageObj = typeof(T) == typeof(string)
                    ? messageValue as T
                    : JsonSerializer.Deserialize<T>(messageValue);

                if (messageObj == null)
                {
                    _logger.LogWarning("Deserialized message is null");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialize failed");
                return false;
            }
        }

        /// <summary>
        /// 錯誤重試次數
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageObj"></param>
        /// <param name="processMessageFunc"></param>
        /// <param name="originalMessage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<bool> ProcessMessageWithRetry<T>(
            T messageObj,
            Func<T, Task> processMessageFunc,
            string originalMessage,
            CancellationToken cancellationToken)
            where T : class
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await processMessageFunc(messageObj);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Retry {Retry}", i + 1);
                    await Task.Delay(500, cancellationToken);
                }
            }

            await SendToDlq(originalMessage);
            return false;
        }

        /// <summary>
        /// 依照 Partition 分組 Commit
        /// - 不同的 partition 要分開 commit
        /// - 每個 partition 只 commit 該 partition 的最大 offset
        /// - Kafka commit 的 offset 是下一筆要讀取的位置，所以要 +1
        /// </summary>
        /// <param name="consumer">Kafka consumer 實例</param>
        /// <param name="batch">待 commit 的訊息批次</param>
        private void CommitByPartition(
            IConsumer<Ignore, string> consumer,
            List<ConsumeResult<Ignore, string>> batch)
        {
            try
            {
                // 不同的 partition 要分開 commit
                var groups = batch.GroupBy(x => x.TopicPartition);

                foreach (var group in groups)
                {
                    var last = group.OrderBy(x => x.Offset.Value).Last();

                    // Kafka commit 是下一筆要開始讀的位置 offset，所以要 +1
                    var offset = new TopicPartitionOffset(
                        last.TopicPartition,
                        last.Offset + 1);

                    consumer.Commit([offset]);
                }
            }
            catch (KafkaException ex)
            {
                _logger.LogError(ex, "Commit failed");
            }
        }

        /// <summary>
        /// Dead Letter Queue，處理失敗的訊息，丟到另一個地方保存
        /// </summary>
        protected async Task SendToDlq(string message)
        {
            // 檢查是否啟用 DLQ
            if (!_settings.EnableDLQ)
            {
                _logger.LogWarning("DLQ is disabled, message will be discarded");
                return;
            }

            if (_producerService == null)
            {
                _logger.LogWarning("Producer service not configured, skipping DLQ");
                return;
            }

            try
            {
                _logger.LogInformation("Sending message to DLQ: {Topic}", _settings.FailedTopic);
                await _producerService.PublishAsync(message, topic: _settings.FailedTopic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DLQ send failed");
            }
        }
    }
}
