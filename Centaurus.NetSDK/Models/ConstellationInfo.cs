using System;
using System.Collections.Generic;
using Centaurus.Models;

namespace Centaurus.NetSDK
{
    public class ConstellationInfo
    {
        public string RawObject { get; set; }

        public ApplicationStateModel State { get; set; }

        private string vault;
        public string Vault 
        {
            get => vault;
            set {
                vault = value;
                VaultPubKey = StrKey.DecodeStellarAccountId(Vault);
            }
        }

        public RawPubKey VaultPubKey { get; private set; }

        private string[] auditors;
        public string[] Auditors 
        {
            get => auditors;
            set {
                if (value == null)
                    throw new ArgumentNullException("Auditors");
                auditors = value;
                var auditorKeys = new List<RawPubKey>();
                foreach (var auditor in value)
                {
                    auditorKeys.Add(StrKey.DecodeStellarAccountId(auditor));
                }
                AuditorPubKeys = auditorKeys.ToArray();
            }
        }

        public RawPubKey[] AuditorPubKeys { get; private set; }

        public long MinAccountBalance { get; set; }

        public long MinAllowedLotSize { get; set; }

        public Network StellarNetwork { get; set; }

        public Asset[] Assets { get; set; }

        public RequestRateLimits RequestRateLimits { get; set; }

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

            public string DisplayName => Issuer == null ? "XLM" : $"{Code}:{Issuer}";
        }

        public enum ApplicationStateModel
        {
            Undefined = 0,
            /// <summary>
            /// First start
            /// </summary>
            WaitingForInit = 1,
            /// <summary>
            /// It has started, but not yet ready. If Alpha, then it waits for the majority to connect. If the Auditor, then it waits for a handshake
            /// </summary>
            Running = 2,
            /// <summary>
            /// Ready to process quanta
            /// </summary>
            Ready = 3,

            Rising = 4,

            Failed = 10
        }
    }
}
