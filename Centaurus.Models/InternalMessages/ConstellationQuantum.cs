using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationQuantum: Quantum
    {
        [XdrField(0)]
        public ConstellationMessageEnvelope RequestEnvelope { get; set; }

        public Message RequestMessage => RequestEnvelope.Message;
    }
}
