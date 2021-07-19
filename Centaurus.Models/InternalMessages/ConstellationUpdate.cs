using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationUpdate : ConstellationRequestMessage
    {        
        [XdrField(0)]
        public List<ProviderSettings> Providers { get; set; }

        [XdrField(1)]
        public List<RawPubKey> Auditors { get; set; }

        [XdrField(2)]
        public ulong MinAccountBalance { get; set; }

        [XdrField(3)]
        public ulong MinAllowedLotSize { get; set; }

        [XdrField(4)]
        public List<AssetSettings> Assets { get; set; }

        [XdrField(5, Optional = true)]
        public RequestRateLimits RequestRateLimits { get; set; }

        public override MessageTypes MessageType => MessageTypes.ConstellationUpdate;
    }
}
