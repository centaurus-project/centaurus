using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class UnauthorizedException : BaseClientException
    {
        public UnauthorizedException()
        {

        }

        public UnauthorizedException(string message)
            :base(message)
        {

        }

        public override ResultStatusCodes StatusCode => ResultStatusCodes.Unauthorized;
    }
}
