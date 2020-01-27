using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantaBatch : Message
    {
        public override MessageTypes MessageType => MessageTypes.QuantaBatch;

        [XdrField(0)]
        public List<MessageEnvelope> Quanta { get; set; }
    }
}
