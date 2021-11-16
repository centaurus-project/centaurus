using System;
using System.Collections.Generic;
using System.Linq;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class SyncQuantaBatch : Message
    {
        [XdrField(0)]
        public List<SyncQuantaBatchItem> Quanta { get; set; }

        [XdrField(2)]
        public ulong LastKnownApex { get; set; }
    }

    [XdrContract]
    public class SyncQuantaBatchItem : IApex
    {
        //TODO: change type to Quantum after migrating to MessagePack
        [XdrField(0)]
        public Message Quantum { get; set; }

        [XdrField(1)]
        public AuditorSignatureInternal AlphaSignature { get; set; }

        public ulong Apex => ((Quantum)Quantum).Apex;
    }
}
