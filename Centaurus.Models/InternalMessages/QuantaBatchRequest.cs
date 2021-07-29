using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantaBatchRequest : Message
    {
        public override MessageTypes MessageType => MessageTypes.QuantaBatchRequest;

        [XdrField(0)]
        public ulong LastKnownApex { get; set; }
    }
}