using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class MessageEnvelope
    {
        [XdrField(0)]
        public Message Message { get; set; }

        //TODO: do not serialize hashes
        //public byte[] Hash { get; set; }

        [XdrField(1)]
        public List<Ed25519Signature> Signatures { get; set; }
    }
}
