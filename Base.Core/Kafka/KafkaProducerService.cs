using Base.Core.Kafka.Interface;
using Base.Core.Model;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Base.Core.Kafka
{
    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly ILogger<KafkaProducerService> _logger;
        private readonly IProducer<string, string> _producer;
        private readonly KafkaSetting _settings;

        public KafkaProducerService(
            ILogger<KafkaProducerService> logger,
            KafkaSetting settings)
        {
            _logger = logger;
            _settings = settings;

            var config = new ProducerConfig
            {
                BootstrapServers = settings.BootstrapServers,
                BatchSize = 102400,  // 單次批量發送的最大位元組限制
                LingerMs = 50,      // 訊息處理不夠快，資料<=100KB，最多等50ms就需要送出
                CompressionType = CompressionType.Snappy // 資料傳送前先做壓縮
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task PublishAsync<T>(
            T message,
            string? key = null,
            string? topic = null)
            where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(message);

                var kafkaMessage = new Message<string, string>
                {
                    Key = key,
                    Value = json
                };

                await _producer.ProduceAsync(topic ?? _settings.Topic, kafkaMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kafka publish failed");
            }
        }

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
    }
}

