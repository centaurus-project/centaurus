using System;

namespace Centaurus.Domain
{
    public static class ExceptionExtensions
    {
        public static ResultStatusCodes GetStatusCode(this Exception exception)
        {
            if (exception is BaseClientException)
                return ((BaseClientException)exception).StatusCode;
            return ResultStatusCodes.InternalError;
        }
    }
}
