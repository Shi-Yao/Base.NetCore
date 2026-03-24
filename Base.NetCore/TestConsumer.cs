using Base.Core.Kafka.Interface;

namespace Base.NetCoreAPI
{
    public class TestConsumer : BackgroundService
    {
        private readonly IKafkaConsumerService _kafkaConsumer;
        private readonly ILogger<TestConsumer> _logger;

        public TestConsumer(
            Dictionary<string, IKafkaConsumerService> kafkaConsumers,
            ILogger<TestConsumer> logger)
        {
            _kafkaConsumer = kafkaConsumers["ddd"];
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Kafka TestConsumer started");

            await _kafkaConsumer.SubscribeAsync<string>(
                async message =>
                {
                    _logger.LogInformation("Received message: {message}", message);

                    // 模擬處理
                    await Task.Delay(100, stoppingToken);
                },
                stoppingToken);
        }
    }
}
