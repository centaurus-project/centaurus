using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public class QuantumResultMessage : ResultMessage, IEffectsContainer
    {
        public override MessageTypes MessageType => MessageTypes.QuantumResultMessage;

        [XdrField(0)]
        public List<Effect> ClientEffects { get; set; }

        [XdrField(1)]
        public EffectsProof Effects { get; set; }
    }
}
