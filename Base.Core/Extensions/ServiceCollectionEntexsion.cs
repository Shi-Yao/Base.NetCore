using Base.Core.Swagger;
using Base.Core.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace Base.Core.Extensions
{
    public static class ServiceCollectionEntexsion
    {
        public static IServiceCollection AddServiceCollection(
            this IServiceCollection services,
            IHostEnvironment env,
            IConfiguration config)
        {

            // 1. 建議使用 Get<T> 直接獲取物件，若設定檔不存在則不執行
            var swaggerDto = config.GetSection("CustSwagger").Get<SwaggerSetting>();

            if (swaggerDto != null)
            {
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

                    // 定義 Bearer 方案
                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "請輸入 Token"
                    });

                    // 設定安全需求
                    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    });
                });
            }

            // JWT Setting
            if (config.GetSection("JwtSettings").Get<string[]>() != null)
            {
                services.AddSingleton<JWTUtil>();
                var accessKey = Encoding.ASCII.GetBytes(config.GetValue<string>("JwtSettings:AccessKey"));
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    })
                    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, x =>
                    {
                        x.RequireHttpsMetadata = false;
                        x.SaveToken = true;
                        x.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(accessKey), // Key
                            ValidIssuer = config.GetValue<string>("JwtSettings:Issuer"),
                            ValidateIssuer = true,      // 驗證簽發者者
                            ValidateAudience = false,   // 驗證接收者
                            ValidateLifetime = true     // 驗證時間
                        };
                    });
            }

            return services;
        }
    }
}

