using Base.NetCoreAPI.Dtos.Response;
using Base.NetCoreAPI.Interface;
using Dapper;


namespace Base.NetCoreAPI.Repositories
{
    public class ProductRespository : IProductRespository
    {
        private readonly DapperDbContext _context;
        private readonly ILogger _logger;

        public ProductRespository(DapperDbContext context, ILogger<ProductRespository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public List<ProductResDto> FindByAll()
        {
            //_logger.LogInformation($"test111");
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
