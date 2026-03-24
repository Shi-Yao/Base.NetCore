using Base.Core.Model;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Base.Core.Kafka.Interface;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Base.Core.Kafka
{
    public class KafkaConsumerService : IKafkaConsumerService
    {
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly KafkaSetting _settings;

        public KafkaConsumerService(
            ILogger<KafkaConsumerService> logger,
            KafkaSetting settings)
        {
            _logger = logger;
            _settings = settings;
        }

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

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(_settings.Topic);

            var partitionChannels = new ConcurrentDictionary<TopicPartition, Channel<ConsumeResult<Ignore, string>>>();
            var workers = new ConcurrentDictionary<TopicPartition, Task>();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(cancellationToken);

                    if (result?.Message == null || result.IsPartitionEOF)
                        continue;

                    var tp = result.TopicPartition;

                    // 依照 Partition 取得對應的 Channel
                    // 如果不存在就建立一個新的（每個 partition 一個 channel）
                    var channel = partitionChannels.GetOrAdd(tp, _ =>
                    {
                        // 建立 bounded channel（最多 1000 筆）
                        // 1. 防止記憶體爆掉
                        // 2. 當處理跟不上時，觸發 Kafka backpressure
                        var ch = Channel.CreateBounded<ConsumeResult<Ignore, string>>(new BoundedChannelOptions(1000)
                        {
                            FullMode = BoundedChannelFullMode.Wait
                        });

                        // 每個 partition 啟動一個專屬 worker（單線處理）
                        // 1. 保證 Kafka partition 順序
                        // 2. 避免 multi-thread 搶同一 partition
                        workers[tp] = Task.Run(() =>
                            PartitionWorker(ch, consumer, processMessageFunc, cancellationToken));

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

                consumer.Close();
            }
        }

        /// <summary>
        /// 同一個 partition 必須維持順序
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="channel"></param>
        /// <param name="consumer"></param>
        /// <param name="processMessageFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task PartitionWorker<T>(
            Channel<ConsumeResult<Ignore, string>> channel,
            IConsumer<Ignore, string> consumer,
            Func<T, Task> processMessageFunc,
            CancellationToken cancellationToken)
            where T : class
        {
            var batch = new List<ConsumeResult<Ignore, string>>(100);

            var lastCommitTime = DateTime.UtcNow;

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
                    continue;
                }

                if (messageObj == null)
                {
                    await SendToDlq(messageValue);
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
                    continue;
                }

                batch.Add(consumeResult);

                // 假設未滿批次commit條件，程式crash，因為還沒commit，程式重啟後會重上一個 offset開始做
                if (batch.Count >= _settings.CommitBatchSize ||
                    (DateTime.UtcNow - lastCommitTime).TotalSeconds >= 5)
                {
                    CommitBatch(consumer, batch);
                    batch.Clear();

                    lastCommitTime = DateTime.UtcNow;
                }
            }

            // flush 最後殘留（一定要）
            if (batch.Count > 0)
            {
                CommitBatch(consumer, batch);
            }
        }

        private void CommitBatch(
            IConsumer<Ignore, string> consumer,
            List<ConsumeResult<Ignore, string>> batch)
        {
            try
            {
                var last = batch[^1];
                consumer.Commit(last);
            }
            catch (KafkaException ex)
            {
                _logger.LogError(ex, "Commit failed");
            }
        }

        /// <summary>
        /// Dead Letter Queue，處理失敗的訊息，丟到另一個地方保存
        /// </summary>
        protected virtual Task SendToDlq(string message)
        {
            _logger.LogError("DLQ Message: {Message}", message);
            return Task.CompletedTask;
        }
    }
}
