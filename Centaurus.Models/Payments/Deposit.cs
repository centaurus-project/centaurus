using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class Deposit
    {
        [XdrField(0)]
        public PaymentResults PaymentResult { get; set; }

        [XdrField(1)]
        public byte[] TransactionHash { get; set; }

        [XdrField(2)]
        public int Asset { get; set; }

        [XdrField(3)]
        public long Amount { get; set; }

        [XdrField(4)]
        public RawPubKey Destination { get; set; }
    }
}
