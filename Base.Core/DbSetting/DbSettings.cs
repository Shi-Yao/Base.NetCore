using Base.Core.DbSetting;

namespace Base.Core
{
    public class DbSettings : IDbSettings
    {
        public string ConfigTemplate { get; set; } = "";
    }
}