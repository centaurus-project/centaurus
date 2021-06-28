using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class WithdrawalRequest : SequentialRequestMessage, ITransaction
    {
        public override MessageTypes MessageType => MessageTypes.WithdrawalRequest;

        [XdrField(0)]
        public string PaymentProvider { get; set; }

        [XdrField(1)]
        public byte[] Transaction { get; set; }
    }
}