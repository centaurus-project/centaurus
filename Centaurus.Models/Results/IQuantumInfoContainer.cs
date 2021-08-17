using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public interface IQuantumInfoContainer
    {
        public ulong Apex { get; }

        public byte[] QuantumHash { get; }

        public PayloadProof PayloadProof { get; }

        public List<EffectsInfoBase> Effects { get; }
    }
}
