using Base.Core.Model;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Base.Core.Kafka.Interface;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Base.Core.Kafka
{
    public class KafkaConsumerService(
        ILogger<KafkaConsumerService> logger,
        KafkaSetting settings,
        IProducer<Null, string> producer) : IKafkaConsumerService
    {
        private readonly ILogger<KafkaConsumerService> _logger = logger;
        private readonly KafkaSetting _settings = settings;
        private readonly IProducer<Null, string> _producer = producer;

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
            var config = new ConsumerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                GroupId = _settings.GroupId,
                EnableAutoCommit = false,
                AutoOffsetReset = _settings.AutoOffsetReset ?? AutoOffsetReset.Latest,
                // consumer 2次poll 超過這時間會剔除Consumer Group，將partition分給別的consumer消費，做rebalance
                MaxPollIntervalMs = 300000,
                SessionTimeoutMs = 10000,
            };

            // 每個partition對應一個channel(確保順序)
            var partitionChannels = new ConcurrentDictionary<TopicPartition, Channel<ConsumeResult<Ignore, string>>>();
            // 每個 partition 對應一個 worker
            var workers = new ConcurrentDictionary<TopicPartition, Task>();
            // commit queue(所有 worker 共用)
            var commitChannel = Channel.CreateUnbounded<ConsumeResult<Ignore, string>>();

            var consumer = new ConsumerBuilder<Ignore, string>(config)
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    // 此段為Rebalance - Partition機制，當有心的consumer加入group or 有consumer掛掉
                    // 停止該 partition 的 channel(避免繼續處理)
                    _logger.LogWarning("Partitions revoked: {Partitions}", partitions);

                    foreach (var p in partitions)
                    {
                        var tp = p.TopicPartition;
                        if (partitionChannels.TryGetValue(tp, out var ch))
                        {
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

            consumer.Subscribe(_settings.Topic);

            // Commit Worker(唯一操作 Kafka consumer 的地方)
            // 避免multi-thread commit造成unthread safe
            var commitWorker = Task.Run(async () =>
            {
                var batch = new List<ConsumeResult<Ignore, string>>();
                var lastCommitTime = DateTime.UtcNow;

                await foreach (var item in commitChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    batch.Add(item);

                    if (batch.Count >= _settings.CommitBatchSize ||
                        (DateTime.UtcNow - lastCommitTime).TotalSeconds >= 5)
                    {
                        CommitByPartition(consumer, batch);
                        batch.Clear();
                        lastCommitTime = DateTime.UtcNow;
                    }
                }

                if (batch.Count > 0)
                    CommitByPartition(consumer, batch);
            }, cancellationToken);

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
                    {
                        // 建立 bounded channel(最多 1000 筆)
                        // 1. 防止記憶體爆掉
                        // 2. 當處理跟不上時，觸發 Kafka backpressure
                        var ch = Channel.CreateBounded<ConsumeResult<Ignore, string>>(new BoundedChannelOptions(1000)
                        {
                            FullMode = BoundedChannelFullMode.Wait
                        });

                        // 每個 partition 啟動一個專屬 worker(單線程)
                        // 1. 保證 Kafka partition 順序
                        // 2. 避免 multi-thread 搶同一 partition
                        workers[tp] = Task.Run(() =>
                            PartitionWorker(ch, commitChannel, processMessageFunc, cancellationToken));

                        return ch;
                    });

                    await channel.Writer.WriteAsync(result, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Consumer stopping...");
            }
            finally
            {
                foreach (var ch in partitionChannels.Values)
                    ch.Writer.Complete();

                await Task.WhenAll(workers.Values);

                commitChannel.Writer.Complete();
                await commitWorker;

                consumer.Close();
            }
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

        private void CommitByPartition(
            IConsumer<Ignore, string> consumer,
            List<ConsumeResult<Ignore, string>> batch)
        {
            try
            {
                // 不同的 parition要分開commit
                var groups = batch.GroupBy(x => x.TopicPartition);

                foreach (var group in groups)
                {
                    var last = group.OrderBy(x => x.Offset).Last();

                    // Kafka commit 是下一筆要開始讀的位置 offset，所以要+1
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
            try
            {
                await _producer.ProduceAsync(_settings.FailedTopic, new Message<Null, string>
                {
                    Value = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DLQ send failed");
            }
        }
    }
}
