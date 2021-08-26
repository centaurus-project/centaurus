using System;
using System.Collections.Generic;
using System.Linq;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantaBatch : Message
    {
        //TODO: change to List<Quantum> after migrating to MessagePack
        [XdrField(0)]
        public List<Message> Quanta { get; set; }

        [XdrField(1, Optional = true)]
        public List<QuantumSignatures> Signatures { get; set; }

        [XdrField(1)]
        public bool HasMorePendingQuanta { get; set; }
    }

    [XdrContract]
    public class QuantumSignatures
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public List<AuditorSignatureInternal> Signatures { get; set; }
    }
}
