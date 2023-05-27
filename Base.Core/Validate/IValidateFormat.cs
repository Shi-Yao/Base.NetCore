using Microsoft.AspNetCore.Mvc.Filters;

namespace Base.Core.Validate
{
    public interface IValidateFormat
    {
        /// <summary>
        /// 驗證格式
        /// </summary>
        /// <param name="context"></param>
        public void Format(ResultExecutingContext context);
    }
}
