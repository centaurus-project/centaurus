using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class PayloadTooLargeException : BaseClientException
    {
        public PayloadTooLargeException()
        {

        }

        public PayloadTooLargeException(string message)
            : base(message)
        {

        }
        public override ResultStatusCode StatusCode => ResultStatusCode.PayloadTooLarge;
    }
}
