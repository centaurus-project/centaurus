using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL
{
    public class UnknownCommitResultException: Exception
    {
        public UnknownCommitResultException(string message, Exception innerException)
            :base(message, innerException)
        {

        }
    }
}
