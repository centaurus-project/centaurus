using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class WithdrawalRequest : SequentialRequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.WithdrawalRequest;

        [XdrField(0)]
        public string Provider { get; set; }

        [XdrField(1)]
        public string Asset { get; set; }

        [XdrField(2)]
        public ulong Amount { get; set; }

        [XdrField(3)]
        public byte[] Destination { get; set; }

        [XdrField(4)]
        public long Fee { get; set; }
    }
}