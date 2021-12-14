using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationQuantum: RequestQuantumBase
    {
        public Message RequestMessage => RequestEnvelope.Message;
    }
}