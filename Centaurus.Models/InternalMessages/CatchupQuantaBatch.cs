using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class CatchupQuantaBatch: Message
    {
        [XdrField(0)]
        public List<CatchupQuantaBatchItem> Quanta { get; set; }

        [XdrField(2)]
        public bool HasMore { get; set; }
    }

    [XdrContract]
    public class CatchupQuantaBatchItem
    {
        //TODO: change type to Quantum after migrating to MessagePack
        [XdrField(0)]
        public Message Quantum { get; set; }

        [XdrField(1)]
        public List<AuditorSignatureInternal> Signatures { get; set; }
    }
}
