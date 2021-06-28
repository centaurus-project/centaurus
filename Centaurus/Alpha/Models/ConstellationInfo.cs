using Centaurus.Models;
using stellar_dotnet_sdk;
using System.Collections.Generic;

namespace Centaurus
{
    public class ConstellationInfo
    {
        public ApplicationState State { get; set; }

        public ProviderSettings[] Providers { get; set; }

        public string[] Auditors { get; set; }

        public long MinAccountBalance { get; set; }

        public long MinAllowedLotSize { get; set; }

        public AssetSettings[] Assets { get; set; }

        public RequestRateLimits RequestRateLimits { get; set; }
    }
}
