using Base.Core.Kafka.Interface;

namespace Base.NetCoreAPI
{
    public class TestConsumer(
        ILogger<TestConsumer> logger,
        IServiceProvider provider) : BackgroundService
    {
        private readonly ILogger<TestConsumer> _logger = logger;
        private readonly IServiceProvider _provider = provider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 從 DI 容器中取得所有註冊的 consumer topics
            using var scope = _provider.CreateScope();
            var kafkaDict = scope.ServiceProvider.GetRequiredService<Dictionary<string, IKafkaConsumerService>>();
            var topics = kafkaDict.Keys.ToArray();

            // 為每個 topic 啟動獨立的 Task
            // 從 kafkaDict 中的 service.Settings 讀取 ConsumerCount
            var tasks = topics.SelectMany(topic =>
            {
                // 從 service 的 Settings 讀取 consumer 數量
                var service = kafkaDict[topic];
                int consumerCount = service.Settings.ConsumerCount;

                return Enumerable.Range(0, consumerCount).Select(consumerId =>
                {
                    _logger.LogInformation("[ConsumerExecute] Creating task for topic: {Topic}, consumer #{ConsumerId}/{Total}",
                        topic, consumerId + 1, consumerCount);

                    // 使用 Task.Run 確保建立後立即返回 Task，不會阻塞主迴圈
                    return Task.Run(async () => await ConsumeTopicAsync(topic, consumerId, consumerCount, stoppingToken));
                });
            }).ToArray();

            // 等待所有 topic 的 consumer 完成 (通常不會結束，直到 stoppingToken 觸發)
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 執行特定 Topic 的訊息消費邏輯。
        /// </summary>
        /// <param name="topic">要消費的 Kafka Topic 名稱。</param>
        /// <param name="consumerId">當前消費者的識別編號 (從 0 開始)。</param>
        /// <param name="totalConsumers">該 Topic 配置的總消費者數量。</param>
        /// <param name="stoppingToken">用於監控應用程式是否停止的權杖。</param>
        /// <returns>代表非同步操作的 Task。</returns>
        private async Task ConsumeTopicAsync(string topic, int consumerId, int totalConsumers, CancellationToken stoppingToken)
        {
            _logger.LogInformation("[Consumer #{ConsumerId}/{Total}] Starting consumer for topic: {Topic}",
                consumerId + 1, totalConsumers, topic);

            try
            {
                using var scope = _provider.CreateScope();
                var kafkaDict = scope.ServiceProvider.GetRequiredService<Dictionary<string, IKafkaConsumerService>>();
                var kafkaConsumer = kafkaDict[topic];

                await kafkaConsumer.SubscribeAsync<string>(async message =>
                {
                    // Do service things

                    _logger.LogInformation("[Consumer #{ConsumerId}/{Total}][{Topic}] Received message: {Message} done",
                        consumerId + 1, totalConsumers, topic, message);

                    await Task.Delay(100, stoppingToken);
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Consumer #{ConsumerId}/{Total}] Consumer for topic {Topic} stopped",
                    consumerId + 1, totalConsumers, topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Consumer #{ConsumerId}/{Total}] Error in consumer for topic {Topic}",
                    consumerId + 1, totalConsumers, topic);
                throw;
            }
        }
    }
}
