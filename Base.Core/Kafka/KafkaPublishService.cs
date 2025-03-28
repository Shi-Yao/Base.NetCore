using Base.Core.Model;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Base.Core.Kafka.Interface;

namespace Base.Core.Kafka
{
    public class KafkaPublishService : IKafkaPublishService
    {
        protected readonly ILogger<KafkaPublishService> _logger;
        protected readonly string _kafkaServer;
        protected readonly string _kafkaTopic;
        protected readonly string _kafkaGroupId;

        public KafkaPublishService(ILogger<KafkaPublishService> logger, KafkaSetting settings)
        {
            _logger = logger;
            _kafkaServer = settings.BootstrapServers;
            _kafkaTopic = settings.Topic;
            _kafkaGroupId = settings.GroupId;
        }


        public async Task PublishAsync<T>(T? message) where T : class
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _kafkaServer,
                BatchSize = 16384,
                LingerMs = 20

            };

            using var producer = new ProducerBuilder<string, string>(config).Build();
            await producer.ProduceAsync(_kafkaTopic, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = JsonConvert.SerializeObject(message)
            });
        }
    }
}
