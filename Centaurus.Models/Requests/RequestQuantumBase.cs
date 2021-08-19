using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class RequestQuantumBase : Quantum
    {
        /// <summary>
        /// Contains original operation request received from a client.
        /// </summary>
        [XdrField(0)]
        public MessageEnvelopeBase RequestEnvelope { get; set; }

        public SequentialRequestMessage RequestMessage => (SequentialRequestMessage)RequestEnvelope.Message;
    }
}