using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)PaymentTypes.Deposit, typeof(Deposit))]
    [XdrUnion((int)PaymentTypes.Withdrawal, typeof(Withdrawal))]
    public abstract class PaymentBase
    {
        public abstract PaymentTypes Type { get; }

        [XdrField(1)]
        public PaymentResults PaymentResult { get; set; }

        [XdrField(2)]
        public byte[] TransactionHash { get; set; }
    }
}
