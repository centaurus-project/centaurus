using System;
using System.Collections.Generic;
using System.Linq;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class AlphaQuantaBatch : Message
    {
        [XdrField(0)]
        public List<AlphaQuantaBatchItem> Quanta { get; set; }

        [XdrField(2)]
        public ulong LastKnownApex { get; set; }
    }

    [XdrContract]
    public class AlphaQuantaBatchItem
    {
        //TODO: change type to Quantum after migrating to MessagePack
        [XdrField(0)]
        public Message Qunatum { get; set; }

        [XdrField(1)]
        public AuditorSignatureInternal AlphaSignature { get; set; }
    }
}
