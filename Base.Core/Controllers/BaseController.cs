using Base.Core.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Base.Core.Controllers
{
    public class BaseController<T> : Controller where T : BaseController<T>
    {
        protected ApiResponseDto _apiResponseDto;

        //public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        //{

        //    await next();
        //}

        protected Task<ApiResponseDto> Success(object o)
        {
            _apiResponseDto.Success(o);
            return Task.FromResult(this._apiResponseDto);
        }

        protected Task<ApiResponseDto> OtherError(string errorMsg)
        {
            this._apiResponseDto.OtherError(errorMsg);
            return Task.FromResult(this._apiResponseDto);
        }

        protected Task<ApiResponseDto> DataNotFound()
        {
            this._apiResponseDto.DataNotFound();
            return Task.FromResult(this._apiResponseDto);
        }
    }
}
