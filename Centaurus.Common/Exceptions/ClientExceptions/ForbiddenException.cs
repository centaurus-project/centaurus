using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class ForbiddenException: BaseClientException
    {
        public override ResultStatusCode StatusCode => ResultStatusCode.Forbidden;
    }
}
