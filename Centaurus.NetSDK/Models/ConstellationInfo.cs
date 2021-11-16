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

        private Asset quoteAsset;
        public Asset QuoteAsset
        {
            get
            {
                if (quoteAsset == null)
                    quoteAsset = Assets?.FirstOrDefault(a => a.IsQuoteAsset);
                return quoteAsset;
            }
        }

        public class Asset
        {
            public string Code { get; set; }

            public bool IsSuspended { get; set; }

            public bool IsQuoteAsset { get; set; }
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

                public bool IsVirtual { get; set; }
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
            /// It has started, but not yet ready. If Alpha, then it waits for the majority to connect. If the Auditor, then it waits for a handshake
            /// </summary>
            Running = 1,
            /// <summary>
            /// Ready to process quanta
            /// </summary>
            Ready = 2,

            Rising = 4,

            Failed = 10
        }
    }
}
