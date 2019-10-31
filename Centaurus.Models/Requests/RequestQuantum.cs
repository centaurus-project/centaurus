using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class RequestQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.RequestQuantum;

        /// <summary>
        /// Contains original operation request received from a client.
        /// </summary>
        [XdrField(0)]
        public MessageEnvelope RequestEnvelope { get; set; }
    }
}
