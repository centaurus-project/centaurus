using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class BadRequestException: BaseClientException
    {
        public BadRequestException()
        {

        }

        public BadRequestException(string message)
            : base(message)
        {

        }

        public override ResultStatusCode StatusCode => ResultStatusCode.BadRequest;
    }
}
