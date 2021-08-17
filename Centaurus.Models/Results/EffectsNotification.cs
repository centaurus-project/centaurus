using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class EffectsNotification : Message, IQuantumInfoContainer
    {

        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public byte[] QuantumHash { get; set; }

        [XdrField(2)]
        public PayloadProof PayloadProof { get; set; }

        [XdrField(3)]
        public List<EffectsInfoBase> Effects { get; set; }
    }
}