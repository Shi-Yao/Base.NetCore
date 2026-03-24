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

        public KafkaTestController(ILogger<KafkaTestController> logger,
            ApiResponseDto apiResponseDto,
            IConfiguration configuration,
            Dictionary<string, IKafkaProducerService> producerDictionary)
        {
            _logger = logger;
            _apiResponseDto = apiResponseDto;
            _configuration = configuration;
            _producer = producerDictionary["ddd"];
        }

        [HttpGet("producer")]
        [ValidateAsyncFilter]
        public async Task InserCustInfo()
        {

            await _producer.PublishAsync("123", "1");
            await _producer.PublishAsync("222", "2");
            await _producer.PublishAsync("333", "3");
            return ;
        }
    }
}
