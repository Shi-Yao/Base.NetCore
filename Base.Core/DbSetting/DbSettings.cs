using Base.Core.DbSetting;
using Microsoft.Extensions.Configuration;

namespace Base.Core
{
    public class DbSettings : IDbSettings
    {
        public string ConfigTemplate { get; set; }

        public DbSettings(IConfiguration configuration)
        {
            ConfigTemplate = configuration.GetConnectionString("ShoppingMartConnection");
        }
    }
}