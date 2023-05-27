using Base.NetCoreAPI.Dtos.Response;

namespace Base.NetCoreAPI.Interface
{
    public interface IProductService
    {
        Task<List<ProductResDto>> QueryProduct(int? prodcutType);
    }
}
