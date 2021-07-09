using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationSettings
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public List<ProviderSettings> Providers { get; set; }

        [XdrField(2)]
        public List<RawPubKey> Auditors { get; set; }

        [XdrField(3)]
        public ulong MinAccountBalance { get; set; }

        [XdrField(4)]
        public ulong MinAllowedLotSize { get; set; }

        [XdrField(5)]
        public List<AssetSettings> Assets { get; set; }

        [XdrField(6, Optional = true)]
        public RequestRateLimits RequestRateLimits { get; set; }
    }
}
