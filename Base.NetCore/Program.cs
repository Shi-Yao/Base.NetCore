using Base.Core;
using Base.Core.DbSetting;
using Base.Core.Dtos;
using Base.Core.Extensions;
using Base.Core.Validate;
using Base.NetCoreAPI;
using Base.NetCoreAPI.Interface;
using Base.NetCoreAPI.Repositories;
using Base.NetCoreAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// 引用共用元件
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddServiceCollection(builder.Environment, builder.Configuration);
builder.Services.RegisterKafkaServices(builder.Configuration, "aaa", "bbb", "ccc", "ddd");
builder.Host.UseCustSerilog(builder.Configuration);

builder.Services.AddScoped<IProductRespository, ProductRespository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IValidateFormat, ValidateFormat>();
builder.Services.AddScoped<ApiResponseDto>();

// DB連線
builder.Services.AddSingleton<IDbSettings, DbSettings>();
builder.Services.AddSingleton<DapperDbContext>();
builder.Services.AddHealthChecks();
var app = builder.Build();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

app.MapHealthChecks("/health");
// Configure the HTTP request pipeline.
app.UseAutoMiddleware(app.Environment, app.Configuration);
app.Run();
