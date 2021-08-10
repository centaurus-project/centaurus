using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public abstract class MessageEnvelopeBase
    {
        [XdrField(0)]
        public Message Message { get; set; }
    }

    public class MessageEnvelope : MessageEnvelopeBase
    {
        [XdrField(0)]
        public TinySignature Signature { get; set; }
    }

    public class ConstellationMessageEnvelope : MessageEnvelopeBase
    {
        [XdrField(0)]
        public List<TinySignature> Signatures { get; set; }
    }
}