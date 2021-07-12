using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public class TransactionResultMessage : QuantumResultMessage
    {
        public override MessageTypes MessageType => MessageTypes.ITransactionResultMessage;

        [XdrField(0)]
        public List<TxSignature> TxSignatures { get; set; } = new List<TxSignature>();
    }
}
