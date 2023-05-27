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

            // Swagger Setting
            if (config.GetSection("CustSwagger").Get<string[]>() != null)
            {
                SwaggerSetting swaggerDto = new SwaggerSetting();
                config.Bind("CustSwagger", swaggerDto);
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Version = swaggerDto.Version,
                        Title = swaggerDto.Title,
                        Description = swaggerDto.Description
                    });

                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer",
                        BearerFormat = "JWT"
                    });

                    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                },
                                },
                            new List<string>()
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

