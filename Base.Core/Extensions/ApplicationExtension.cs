using Base.Core.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Base.Core.Extensions
{
    public static class ApplicationExtension
    {
        public static IApplicationBuilder UseAutoMiddleware(this IApplicationBuilder app, IHostEnvironment env, IConfiguration config)
        {
            app.UseMiddleware<ApiExceptionMiddleware>();
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRouting();

            // JWT Setting
            if (config.GetSection("JwtSettings").Get<string[]>() != null)
            {
                app.UseAuthentication();
                app.UseAuthorization();
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            return app;
        }
    }
}
