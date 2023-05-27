using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Display;

namespace Base.Core.Extensions
{
    public static class SerilogExtension
    {
        public static IHostBuilder UseCustSerilog(this IHostBuilder builder, IConfiguration config)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config, "CustSerilog")
                .Enrich.FromLogContext() // 自定義LOG
                //.WriteTo.Console(new MessageTemplateTextFormatter("{Timestamp:HH:mm:ss} [{Level:u3}] {Message}{NewLine}{Exception}"))
                .CreateLogger();

            builder.UseSerilog();

            return builder;
        }

    }
}
