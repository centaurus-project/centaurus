using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class EffectsResponse : ResultMessage
    {
        public override MessageTypes MessageType => MessageTypes.EffectsResponse;

        [XdrField(0)]
        public List<Effect> Items { get; set; }

        [XdrField(1)]
        public string CurrentToken { get; set; }

        [XdrField(2)]
        public string PrevToken { get; set; }

        [XdrField(3)]
        public string NextToken { get; set; }
    }
}
