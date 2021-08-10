using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public abstract class QuantumResultMessageBase : ResultMessageBase, IEffectsContainer
    {
        [XdrField(0)]
        public List<Effect> ClientEffects { get; set; }

        [XdrField(1)]
        public EffectsProof Effects { get; set; }

        public Quantum Quantum => (Quantum)OriginalMessage.Message;

        public ulong Apex => Quantum.Apex;
    }
}