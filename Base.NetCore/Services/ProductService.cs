using Base.Core.Enums;
using Base.Core.Exceptions;
using Base.Core.Extensions;
using Base.NetCoreAPI.Dtos.Response;
using Base.NetCoreAPI.Interface;

namespace Base.NetCoreAPI.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRespository _productRespository;
        public ProductService(IProductRespository productRespository)
        {
            _productRespository = productRespository;
        }

        public async Task<List<ProductResDto>> QueryProduct(int? prodcutType)
        {
            List<ProductResDto> result;
            if (prodcutType == null)
            {
                result = _productRespository.FindByAll();
            }
            else
            {
                result = _productRespository.FindByProductType(prodcutType);
            }

            if(result.Count == 0)
            {
                throw new BadRequestException(
                    ReturnCode.DataNotFound.GetDisplayName(),
                    ReturnCode.DataNotFound.GetDescription());
            }

            return result;
        }
    }
}
