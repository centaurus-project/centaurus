using Centaurus.Models;

namespace Centaurus
{
    public class ConstellationInfo
    {
        public ApplicationState State { get; set; }

        public ProviderSettings[] Providers { get; set; }

        public string[] Auditors { get; set; }

        public ulong MinAccountBalance { get; set; }

        public ulong MinAllowedLotSize { get; set; }

        public AssetSettings[] Assets { get; set; }

        public RequestRateLimits RequestRateLimits { get; set; }
    }
}
