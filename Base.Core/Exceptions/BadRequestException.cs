namespace Base.Core.Exceptions
{
    public class BadRequestException : Exception
    {
        public virtual string? _returnCode { get; set; }
        public virtual string? _returnMessage { get; set; }
        public BadRequestException(string returnCode, string returnMessage)
        {
            this._returnCode = returnCode;
            this._returnMessage = returnMessage;
        }
    }
}
