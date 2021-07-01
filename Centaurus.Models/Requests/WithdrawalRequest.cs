using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class WithdrawalRequest : SequentialRequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.WithdrawalRequest;

        [XdrField(0)]
        public string PaymentProvider { get; set; }

        [XdrField(1)]
        public int Asset { get; set; }

        [XdrField(2)]
        public long Amount { get; set; }

        [XdrField(3)]
        public string Destination { get; set; }

        [XdrField(4)]
        public long Fee { get; set; }
    }
}