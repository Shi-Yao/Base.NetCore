using Base.NetCoreAPI.Dtos.Response;

namespace Base.NetCoreAPI.Interface
{
    public interface IProductRespository
    {
        public List<ProductResDto> FindByAll();

        public List<ProductResDto> FindByProductType(int? productType);
    }
}
