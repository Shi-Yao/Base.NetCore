using Base.Core.Dtos;
using Base.Core.Filters;
using Base.Core.Kafka.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Base.NetCoreAPI.Controllers
{
    [ApiController]
    [Route("/kafka")]
    public class KafkaProducerController
    {
        private readonly ILogger<KafkaProducerController> _logger;
        private readonly ApiResponseDto _apiResponseDto;
        private readonly IConfiguration _configuration;
        private readonly IKafkaProducerService _producer;


        public KafkaProducerController(ILogger<KafkaProducerController> logger,
            ApiResponseDto apiResponseDto,
            IConfiguration configuration,
            Dictionary<string, IKafkaProducerService> producerDictionary)
        {
            _logger = logger;
            _apiResponseDto = apiResponseDto;
            _configuration = configuration;
            _producer = producerDictionary["ccc"];
        }

        [HttpGet]
        [ValidateAsyncFilter]
        public async Task InserCustInfo()
        {

            await _producer.PublishAsync("123");
            return ;
        }
    }
}
