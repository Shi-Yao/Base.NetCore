using Base.NetCoreAPI.Dtos.Response;
using Base.NetCoreAPI.Interface;
using Dapper;

namespace Base.NetCoreAPI.Repositories
{
    public class ProductRespository : IProductRespository
    {
        private readonly DapperDbContext _context;
        public ProductRespository(DapperDbContext context)
        {
            _context = context;
        }

        public List<ProductResDto> FindByAll()
        {
            var sql = "SELECT * FROM ShoppingMart.dbo.Product";
            using (var connection = _context.CreateConnection())
            {
                var result = connection.Query<ProductResDto>(sql);
                return result.ToList();
            }
        }

        public List<ProductResDto> FindByProductType(int? productType)
        {
            var sql = "SELECT * FROM ShoppingMart.dbo.Product WHERE productType = @productType";
            using (var connection = _context.CreateConnection())
            {
                var result = connection.Query<ProductResDto>(
                    sql,
                    new { productType });
                return result.ToList();
            }
        }
    }
}
