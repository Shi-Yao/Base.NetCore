using Base.Core.Enums;
using Base.Core.Exceptions;
using Base.Core.Extensions;
using Base.NetCoreAPI.Dtos.Response;
using Base.NetCoreAPI.Interface;
using Base.NetCoreAPI.Services;
using NSubstitute;

namespace Base.NetCoreAPI.Test
{
    [TestFixture]
    public class BaseNetCoreAPITest
    {
        private IProductRespository _productRespository;
        private IProductService _productService;

        [SetUp]
        public void Setup()
        {
            _productRespository = Substitute.For<IProductRespository>();
            _productService = new ProductService(_productRespository);
        }

        [Test]
        public async Task query_product_when_productType_is_null()
        {
            var data = new List<ProductResDto> {
                new ProductResDto() { ProductID = 1, ProductName = "A", ProductPrice = 10, ProductType = 1},
                new ProductResDto() { ProductID = 2, ProductName = "B", ProductPrice = 20, ProductType = 2}
            };
            _productRespository.FindByAll().Returns(data);

            var result = await _productService.QueryProduct(null);

            Assert.That(result.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task query_product_when_productType_is_one()
        {
            var data = new List<ProductResDto> {
                new ProductResDto() { ProductID = 1, ProductName = "A", ProductPrice = 10, ProductType = 1}
            };
            _productRespository.FindByAll().Returns(data);

            var result = await _productService.QueryProduct(null);

            Assert.That(result.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task query_product_when_productType_is_not_exist()
        {
            _productRespository.FindByAll().Returns(new List<ProductResDto>() { });
            var errorMessage = ReturnCode.DataNotFound.GetDescription();
            var errorDisplayName = ReturnCode.DataNotFound.GetDisplayName();

            var ex = Assert.ThrowsAsync<BadRequestException>(async () => await _productService.QueryProduct(null));

            Assert.That(ex._returnMessage, Is.EqualTo(errorMessage));
            Assert.That(ex._returnCode, Is.EqualTo(errorDisplayName));
        }
    }
}