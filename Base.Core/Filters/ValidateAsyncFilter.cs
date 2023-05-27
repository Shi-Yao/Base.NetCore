using Base.Core.Validate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Base.Core.Filters
{
    public class ValidateAsyncFilter : ActionFilterAttribute
    {

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            await next();
        }

        public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (!context.ModelState.IsValid)
            {
                try
                {
                    var formatHandler = context.HttpContext.RequestServices.GetService<IValidateFormat>();
                    // 因context.Result有值，故放在OnResultExecutionAsync處理
                    formatHandler.Format(context);
                }
                catch
                {
                    context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            await next();

        }

    }
}
