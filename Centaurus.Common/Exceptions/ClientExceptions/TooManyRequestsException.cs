using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class TooManyRequestsException : BaseClientException
    {
        public TooManyRequestsException()
        {

        }

        public TooManyRequestsException(string message)
            : base(message)
        {

        }


        public override ResultStatusCodes StatusCode => ResultStatusCodes.TooManyRequests;
    }
}
