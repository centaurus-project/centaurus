using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class PendingQuantum : Message
    {
        //TODO: replace with Quantum after migrating to MessagePack
        [XdrField(0)]
        public Message Message { get; set; }

        public Quantum Quantum
        {
            get { return (Quantum)Message; }
            set { Message = value; }
        }

        [XdrField(1)]
        public List<AuditorSignatureInternal> Signatures { get; set; }
    }
}
