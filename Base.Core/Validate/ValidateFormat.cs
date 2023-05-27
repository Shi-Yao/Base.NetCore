using Base.Core.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Base.Core.Validate
{
    public class ValidateFormat : IValidateFormat
    {
        public void Format(ResultExecutingContext context)
        {
            var message = string.Join(" , ", context.ModelState.Values.SelectMany(s => s.Errors).Select(e => e.ErrorMessage));
            ApiResponseDto apiResponseDto = new ApiResponseDto();
            apiResponseDto.ParaFormatError(message);
            context.Result = new BadRequestObjectResult(apiResponseDto);
        }
    }
}
