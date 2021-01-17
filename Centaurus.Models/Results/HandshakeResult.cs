using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class HandshakeResult : ResultMessage
    {
        public override MessageTypes MessageType => MessageTypes.HandshakeResult;

        [XdrField(0)]
        public int AccountId { get; set; }
    }
}
