using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)PaymentTypes.Deposit, typeof(Deposit))]
    [XdrUnion((int)PaymentTypes.Withdrawal, typeof(Withdrawal))]
    public abstract class PaymentBase
    {
        public abstract PaymentTypes Type { get; }

        [XdrField(0)]
        public PaymentResults PaymentResult { get; set; }

        [XdrField(1)]
        public int Asset { get; set; }

        [XdrField(2)]
        public long Amount { get; set; }

        [XdrField(3)]
        public RawPubKey Destination { get; set; }

        [XdrField(4)]
        public byte[] TransactionHash { get; set; }
    }
}
