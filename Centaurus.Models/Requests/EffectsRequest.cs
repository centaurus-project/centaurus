using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class EffectsRequest : RequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.EffectsRequest;

        public override ulong MessageId => PagingToken.GetInt64HashCode();

        [XdrField(0)]
        public string PagingToken { get; set; }
    }
}