using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class ClientExceptionHelper
    {
        public static ResultStatusCodes GetExceptionStatusCode(Exception exception)
        {
            var resultStatusCode = ResultStatusCodes.InternalError;
            if (exception is InvalidStateException)
                resultStatusCode = ResultStatusCodes.InvalidState;
            else if (exception is UnauthorizedException)
                resultStatusCode = ResultStatusCodes.Unauthorized;
            else if (exception is UnexpectedMessageException)
                resultStatusCode = ResultStatusCodes.UnexpectedMessage;
            else if (exception is BadRequestException)
                resultStatusCode = ResultStatusCodes.BadRequest;

            return resultStatusCode;
        }
    }
}
