using Centaurus.Xdr;
using System;
using System.Linq;

namespace Centaurus.Models
{
    public class PaymentRequest : SequentialRequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.PaymentRequest;

        [XdrField(0)]
        public int Asset { get; set; }

        [XdrField(1)]
        public long Amount { get; set; }

        [XdrField(2)]
        public RawPubKey Destination { get; set; }
    }
}
