using Base.Core.Enums;
using Base.Core.Extensions;
using System.Text.Json.Serialization;

namespace Base.Core.Dtos
{
    public class ApiResponseDto
    {
        public string returnCode { get; set; }
        public string returnMessage { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object data { get; set; }

        public ApiResponseDto Success(object data = default)
        {
            this.data = data;
            this.returnCode = ReturnCode.Success.GetDisplayName();
            this.returnMessage = ReturnCode.Success.GetDescription();
            return this;
        }

        public ApiResponseDto DataNotFound()
        {
            this.returnCode = ReturnCode.DataNotFound.GetDisplayName();
            this.returnMessage = ReturnCode.DataNotFound.GetDescription();
            return this;
        }

        public ApiResponseDto ParaFormatError(string errorMsg)
        {
            this.returnCode = ReturnCode.ParaFormatError.GetDisplayName();
            this.returnMessage = ReturnCode.ParaFormatError.GetDescription() + ": " + errorMsg;
            return this;
        }

        public ApiResponseDto OtherError(string errorMsg)
        {
            this.returnCode = ReturnCode.OtherError.GetDisplayName();
            this.returnMessage = ReturnCode.OtherError.GetDescription() + ": " + errorMsg;
            return this;
        }
    }
}
