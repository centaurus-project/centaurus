using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public class ITransactionResultMessage : ResultMessage
    {
        public override MessageTypes MessageType => MessageTypes.ITransactionResultMessage;

        [XdrField(0)]
        public List<Ed25519Signature> TxSignatures { get; set; } = new List<Ed25519Signature>();
    }
}
