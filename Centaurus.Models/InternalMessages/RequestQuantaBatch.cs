using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class RequestQuantaBatch : Message
    {
        [XdrField(0)]
        public List<MessageEnvelopeBase> Requests { get; set;}
    }
}
