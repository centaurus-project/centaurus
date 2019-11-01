using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class SnapshotQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.SnapshotQuantum;

        [XdrField(0)]
        public byte[] Hash { get; set; }
    }
}
