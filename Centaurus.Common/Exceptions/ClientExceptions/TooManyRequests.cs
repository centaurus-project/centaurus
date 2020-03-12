using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class TooManyRequests : BaseClientException
    {
        public TooManyRequests()
        {

        }

        public TooManyRequests(string message)
            : base(message)
        {

        }
    }
}
