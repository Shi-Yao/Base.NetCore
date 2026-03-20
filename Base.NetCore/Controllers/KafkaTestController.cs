using Base.Core.Dtos;
using Base.Core.Filters;
using Base.Core.Kafka.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Base.NetCoreAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class KafkaTestController
    {
        private readonly ILogger<KafkaTestController> _logger;
        private readonly ApiResponseDto _apiResponseDto;
        private readonly IConfiguration _configuration;
        private readonly IKafkaProducerService _producer;
        private readonly IKafkaConsumerService _consumer;


        public KafkaTestController(ILogger<KafkaTestController> logger,
            ApiResponseDto apiResponseDto,
            IConfiguration configuration,
            Dictionary<string, IKafkaProducerService> producerDictionary,
            Dictionary<string, IKafkaConsumerService> consumerDictionary)
        {
            _logger = logger;
            _apiResponseDto = apiResponseDto;
            _configuration = configuration;
            _producer = producerDictionary["ccc"];
            _consumer = consumerDictionary["ccc"];
        }

        [HttpGet("producer")]
        [ValidateAsyncFilter]
        public async Task InserCustInfo()
        {

            await _producer.PublishAsync("123");
            return ;
        }

        [HttpGet("consumer")]
        [ValidateAsyncFilter]
        public async Task<string> ReceiveCustInfo()
        {
            // 建立一個 5 秒後自動取消的 Token
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            _logger.LogInformation("開始測試監聽 Kafka 5 秒...");

            try
            {
                await _consumer.SubscribeAsync<string>(async (msg) =>
                {
                    _logger.LogInformation($"[測試成功] 收到訊息: {msg}");
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("監聽測試時間到，已自動結束。");
            }

            return "測試結束，請查看 Log。";
        }
    }
}
