using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.NetSDK
{
    public class ConstellationInfo
    {
        public string RawObject { get; set; }

        public StateModel State { get; set; }

        public List<ProviderSettings> Providers { get; set; }

        public List<Auditor> Auditors { get; set; }

        public ulong MinAccountBalance { get; set; }

        public ulong MinAllowedLotSize { get; set; }

        public List<Asset> Assets { get; set; }

        public RequestRateLimits RequestRateLimits { get; set; }

        public Asset QuoteAsset => Assets.First();

        public class Asset
        {
            public string Code { get; set; }

            public bool IsSuspended { get; set; }
        }

        public class ProviderSettings
        {
            public string Provider { get; set; }

            public string Name { get; set; }

            public string Vault { get; set; }

            public List<ProviderAsset> Assets { get; set; }

            public string Id => PaymentProviderHelper.GetProviderId(Provider, Name);

            public class ProviderAsset
            {
                public string Token { get; set; }

                public string CentaurusAsset { get; set; }
            }
        }

        public class Auditor
        {
            public string Address { get; set; }

            public string PubKey { get; set; }
        }

        public enum StateModel
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
