using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class RequestQuantumBase: Quantum
    {
        /// <summary>
        /// Contains original operation request.
        /// </summary>
        [XdrField(0)]
        public MessageEnvelopeBase RequestEnvelope { get; set; }
    }
}
