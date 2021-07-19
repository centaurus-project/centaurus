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
        public string Asset { get; set; }

        [XdrField(3)]
        public ulong Amount { get; set; }

        [XdrField(4)]
        public ulong Destination { get; set; }
    }
}
