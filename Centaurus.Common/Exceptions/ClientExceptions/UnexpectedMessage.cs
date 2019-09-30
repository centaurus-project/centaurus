using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class UnexpectedMessageException: BaseClientException
    {
        public UnexpectedMessageException(string message)
            :base(message)
        {

        }
    }
}
