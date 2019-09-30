using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class SnapshotQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.SnapshotQuantum;

        public byte[] Hash { get; set; }
    }
}
