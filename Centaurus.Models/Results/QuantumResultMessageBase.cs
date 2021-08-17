using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public abstract class QuantumResultMessageBase : ResultMessageBase, IQuantumInfoContainer
    {
        [XdrField(0)]
        public List<EffectsInfoBase> Effects { get; set; }

        [XdrField(1)]
        public PayloadProof PayloadProof { get; set; }

        public Quantum Quantum => (Quantum)OriginalMessage.Message;

        public ulong Apex => Quantum.Apex;

        public byte[] QuantumHash => Quantum.ComputeHash();
    }
}