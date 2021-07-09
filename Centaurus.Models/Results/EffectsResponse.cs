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
        public List<ApexEffects> Items { get; set; }

        [XdrField(1)]
        public string CurrentPagingToken { get; set; }

        [XdrField(2)]
        public string NextPageToken { get; set; }

        [XdrField(3)]
        public string PrevPageToken { get; set; }

        [XdrField(4)]
        public string Order { get; set; }

        [XdrField(5)]
        public int Limit { get; set; }
    }

    [XdrContract]
    public class ApexEffects
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public List<Effect> Items { get; set; }

        [XdrField(2)]
        public EffectsProof Proof { get; set; }
    }
}
