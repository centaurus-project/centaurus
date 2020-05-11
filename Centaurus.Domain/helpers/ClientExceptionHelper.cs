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
            switch (exception)
            {
                case InvalidStateException exc:
                    return ResultStatusCodes.InvalidState;
                case UnauthorizedException exc:
                    return ResultStatusCodes.Unauthorized;
                case UnexpectedMessageException exc:
                    return ResultStatusCodes.UnexpectedMessage;
                case BadRequestException exc:
                    return ResultStatusCodes.BadRequest;
                case ForbiddenException exc:
                    return ResultStatusCodes.Forbidden;
                default:
                    return ResultStatusCodes.InternalError;
            }
        }
    }
}
