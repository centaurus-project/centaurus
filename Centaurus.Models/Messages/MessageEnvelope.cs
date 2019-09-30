using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class MessageEnvelope: IXdrSerializableModel
    {
        public Message Message { get; set; }

        //TODO: do not serialize hashes
        //public byte[] Hash { get; set; }

        public List<Ed25519Signature> Signatures { get; set; }
    }
}
