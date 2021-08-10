using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public class QuantumResultMessage : QuantumResultMessageBase
    {
        public override MessageTypes MessageType => MessageTypes.QuantumResultMessage;
    }
}