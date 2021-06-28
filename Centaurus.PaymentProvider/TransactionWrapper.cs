using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public class TransactionWrapper
    {
        public virtual object Transaction { get; set; }

        public byte[] Hash { get; set; }

        public long MaxTime { get; set; }
    }
}
