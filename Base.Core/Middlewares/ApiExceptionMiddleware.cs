using Base.Core.Dtos;
using Base.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Base.Core.Middlewares
{
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);

                if (httpContext.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    ApiResponseDto apiResponseDto = new ApiResponseDto()
                    {
                        returnMessage = "Unauthorized",
                    };
                    await httpContext.Response.WriteAsync(JsonSerializer.Serialize(apiResponseDto));
                }
            }
            catch (BadRequestException ex)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await HandleBadRequestExceptionAsvnc(httpContext, ex);
            }
            catch (Exception ex)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await HandleBaseExceptionAsvnc(httpContext, ex);
            }
        }

        public async Task HandleBadRequestExceptionAsvnc(HttpContext httpContext, BadRequestException ex)
        {
            httpContext.Response.ContentType = "application/json";

            _logger.LogError(ex.ToString());
            ApiResponseDto apiResponseDto = new ApiResponseDto()
            {
                returnCode = ex._returnCode,
                returnMessage = ex._returnMessage,
            };

            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(apiResponseDto));
        }

        public async Task HandleBaseExceptionAsvnc(HttpContext httpContext, Exception ex)
        {
            httpContext.Response.ContentType = "application/json";

            _logger.LogError(ex.ToString());
            ApiResponseDto apiResponseDto = new ApiResponseDto();
            string result = JsonSerializer.Serialize(apiResponseDto.OtherError(ex.Message));
            await httpContext.Response.WriteAsync(result);
        }
    }
}
