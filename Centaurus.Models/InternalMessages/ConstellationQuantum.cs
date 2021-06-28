using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationQuantum: Quantum
    {
        public override MessageTypes MessageType => MessageTypes.ConstellationQuantum;

        [XdrField(0)]
        public MessageEnvelope RequestEnvelope { get; set; }

        public Message RequestMessage => RequestEnvelope.Message;
    }
}
