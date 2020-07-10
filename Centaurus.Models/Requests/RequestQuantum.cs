using System;
using System.Collections.Generic;
using Centaurus.Xdr;

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

        public NonceRequestMessage RequestMessage
        {
            get
            {
                return (NonceRequestMessage)RequestEnvelope.Message;
            }
        }
    }
}
