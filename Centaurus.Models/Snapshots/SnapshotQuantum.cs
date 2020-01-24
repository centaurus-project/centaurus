using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class SnapshotQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.SnapshotQuantum;

        [XdrField(0)]
        public byte[] Hash { get; set; }
    }
}
