using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Alpha
{
    public class ConstellationInfo
    {
        public ApplicationState State { get; set; }

        public string Vault { get; set; }

        public string[] Auditors { get; set; }

        public long MinAccountBalance { get; set; }

        public long MinAllowedLotSize { get; set; }

        public Network StellarNetwork { get; set; }

        public Asset[] Assets { get; set; }

        public RequestRateLimitsModel RequestRateLimits { get; set; }

        public class Network
        {
            public Network(string passphrase, string horizon)
            {
                Passphrase = passphrase;
                Horizon = horizon;
            }

            public string Passphrase { get; set; }

            public string Horizon { get; set; }
        }

        public class Asset
        {
            public string Code { get; set; }

            public string Issuer { get; set; }

            public int Id { get; set; }

            public static Asset FromAssetSettings(AssetSettings assetSettings)
            {
                return new Asset
                {
                    Id = assetSettings.Id,
                    Code = assetSettings.Code,
                    Issuer = assetSettings.IsXlm ? null : ((KeyPair)assetSettings.Issuer).AccountId
                };
            }
        }
    }
}
