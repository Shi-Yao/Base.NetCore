using Base.Core.Model;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Base.Core.Kafka.Interface;

namespace Base.Core.Kafka
{
    public class KafkaSubscribeService: IKafkaSubscribeService
    {
        protected readonly ILogger<KafkaSubscribeService> _logger;
        protected readonly string _kafkaServer;
        protected readonly string _kafkaTopic;
        protected readonly string _kafkaGroupId;
        protected readonly AutoOffsetReset _autoOffset;

        public KafkaSubscribeService(ILogger<KafkaSubscribeService> logger, KafkaSetting settings)
        {
            _logger = logger;
            _kafkaServer = settings.BootstrapServers;
            _kafkaTopic = settings.Topic;
            _kafkaGroupId = settings.GroupId;
            _autoOffset = settings.AutoOffsetReset != null ? settings.AutoOffsetReset : AutoOffsetReset.Latest;
        }

        public async Task SubscribeAsync<T>(Func<T, Task> ProcessMessageFunc, CancellationToken cancellationToken = default)
            where T : class
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _kafkaServer,
                GroupId = _kafkaGroupId,
                EnableAutoCommit = false,
                Acks = Acks.Leader,
                AutoOffsetReset = _autoOffset
            };

            using var _ = _logger.BeginScope(new KeyValuePair<string, object>("KafkaServer", _kafkaServer));
            using var __ = _logger.BeginScope(new KeyValuePair<string, object>("KafkaTopic", _kafkaTopic));
            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(_kafkaTopic);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(cancellationToken);
                        if (consumeResult?.Message == null) continue;
                        if (consumeResult.IsPartitionEOF) continue;

                        _logger.LogDebug($"Message received, partition:{consumeResult.Partition}, offset {consumeResult.Offset}.");

                        var messageValue = consumeResult.Message.Value;
                        if (messageValue == null)
                        {
                            _logger.LogError("Received null message.");
                            continue;
                        }

                        try
                        {
                            var messageObj = typeof(T) == typeof(string)
                                ? messageValue as T
                                : JsonConvert.DeserializeObject<T>(messageValue);

                            if (messageObj == null)
                            {
                                _logger.LogError("Failed to deserialize message to {T}", typeof(T));
                                continue;
                            }

                            await ProcessMessageFunc(messageObj);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Message processing failed: {Message}, Error: {Error}", messageValue, ex);
                        }

                        try
                        {
                            consumer.Commit(consumeResult);
                        }
                        catch (KafkaException e)
                        {
                            _logger.LogError(e.Message);
                        }
                    }
                    catch (ConsumeException e)
                    {
                        _logger.LogError("Consume error: {Reason}", e.Error.Reason);
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                _logger.LogWarning("Closing consumer. {Exception}", e);
                consumer.Close();
            }

            await Task.CompletedTask;
        }
    }
}
