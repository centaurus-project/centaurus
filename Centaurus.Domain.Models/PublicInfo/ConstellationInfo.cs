using Centaurus.Models;

namespace Centaurus.Domain.Models
{
    public class ConstellationInfo
    {
        public ulong Apex { get; set; }

        public string PubKey { get; set; }

        public State State { get; set; }

        public ProviderSettings[] Providers { get; set; }

        public Auditor[] Auditors { get; set; }

        public ulong MinAccountBalance { get; set; }

        public ulong MinAllowedLotSize { get; set; }

        public AssetSettings[] Assets { get; set; }

        public RequestRateLimits RequestRateLimits { get; set; }

        public class Auditor
        {
            public string PubKey { get; set; }

            public string Address { get; set; }
        }
    }
}
