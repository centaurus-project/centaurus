using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class PaymentRequestBase : NonceRequestMessage, ITransactionContainer
    {
        [XdrField(0)]
        public int Asset { get; set; }

        [XdrField(1)]
        public long Amount { get; set; }

        [XdrField(2)]
        public RawPubKey Destination { get; set; }

        [XdrField(3)]
        public string Memo { get; set; } = string.Empty;

        [XdrField(4)]
        public byte[] TransactionXdr { get; set; }

        [XdrField(5)]
        public byte[] TransactionHash { get; set; }
    }
}