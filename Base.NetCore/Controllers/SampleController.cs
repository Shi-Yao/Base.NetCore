using Base.Core.Controllers;
using Base.Core.Dtos;
using Base.Core.Filters;
using Base.NetCoreAPI.Dtos.Request;
using Base.NetCoreAPI.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Base.NetCore.Controllers
{
    [ApiController]
    [Route("/product")]
    public class SampleController : Controller
    {
        private readonly ILogger<SampleController> _logger;
        private readonly IProductService _productService;
        private readonly ApiResponseDto _apiResponseDto;
        public SampleController(ILogger<SampleController> logger, IProductService productService, ApiResponseDto apiResponseDto)
        {
            _logger = logger;
            _productService = productService;
            _apiResponseDto = apiResponseDto;
        }

        [HttpGet]
        [Authorize]
        [ValidateAsyncFilter]
        public async Task<ApiResponseDto> QueryProduct([FromQuery]ProductReqDto product)
        {
           var result = await _productService.QueryProduct(product.ProductType);
            _apiResponseDto.Success(result);
            return _apiResponseDto;
        }
    }
}
