using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class UnauthorizedException : BaseClientException
    {
        public override ResultStatusCodes StatusCode => ResultStatusCodes.Unauthorized;
    }
}
