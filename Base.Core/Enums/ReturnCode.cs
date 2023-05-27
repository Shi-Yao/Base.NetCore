using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Base.Core.Enums
{
    public enum ReturnCode
    {
        [Display(Name="0000")]
        [Description("交易成功")]
        Success,

        [Display(Name = "0001")]
        [Description("查無資料")]
        DataNotFound,

        [Display(Name = "0002")]
        [Description("輸入格式有誤")]
        ParaFormatError,

        [Display(Name = "9999")]
        [Description("其他錯誤")]
        OtherError,
    }
}
