using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class ClientRequestQuantumBase : RequestQuantumBase
    {
        public SequentialRequestMessage RequestMessage => (SequentialRequestMessage)RequestEnvelope.Message;
    }
}