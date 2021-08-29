using System;

namespace Centaurus.Domain
{
    public static class ExceptionExtensions
    {
        public static ResultStatusCode GetStatusCode(this Exception exception)
        {
            if (exception is BaseClientException)
                return ((BaseClientException)exception).StatusCode;
            return ResultStatusCode.InternalError;
        }
    }
}
