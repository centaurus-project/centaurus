using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public abstract class BaseClientException: Exception
    {
        public BaseClientException()
        {

        }

        public BaseClientException(string message)
            :base(message)
        {

        }

        public abstract ResultStatusCode StatusCode { get; }
    }
}
