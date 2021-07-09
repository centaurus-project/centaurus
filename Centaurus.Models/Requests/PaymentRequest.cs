using Centaurus.Xdr;
using System;
using System.Linq;

namespace Centaurus.Models
{
    public class PaymentRequest : SequentialRequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.PaymentRequest;

        [XdrField(0)]
        public string Asset { get; set; }

        [XdrField(1)]
        public ulong Amount { get; set; }

        [XdrField(2)]
        public RawPubKey Destination { get; set; }
    }
}
