using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class PaymentRequestBase : NonceRequestMessage, ITransactionContainer
    {
        [XdrField(0)]
        public byte[] TransactionXdr { get; set; }
    }
}