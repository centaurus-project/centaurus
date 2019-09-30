using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class BaseClientException: Exception
    {
        public BaseClientException()
        {

        }

        public BaseClientException(string message)
            :base(message)
        {

        }
    }
}
