using Centaurus.Domain;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Centaurus.Test
{
    public class IntegrationTestEnvironment : IDisposable
    {
        public IntegrationTestEnvironment()
        {
            Reset = new ManualResetEvent(false);
        }

        public const string HorizonUrl = "https://horizon-testnet.stellar.org";
        public const string NetworkPassphrase = "Test SDF Network ; September 2015";
        public const int AlphaPort = 5000;

        public ManualResetEvent Reset { get; }

        public StartupWrapper AlphaWrapper => AuditorWrappers.FirstOrDefault().Value;

        public Dictionary<string, StartupWrapper> AuditorWrappers { get; private set; } = new Dictionary<string, StartupWrapper>();
        public List<KeyPair> Clients { get; } = new List<KeyPair>();

        public void Init(int auditorsCount)
        {
            if (isInited)
                throw new InvalidOperationException("Already inited");

            GenerateAuditors(auditorsCount);

            isInited = true;
        }

        private void GenerateAuditors(int auditorsCount)
        {
            var keyPairs = Enumerable.Range(0, auditorsCount).Select(_ => KeyPair.Random()).ToArray();
            var alpha = keyPairs.First();
            var auditors = keyPairs.Select(k => new Settings.Auditor(k, $"{k.AccountId}.com")).ToList();
            foreach (var kp in keyPairs)
            {
                var host = auditors.First(a => a.PubKey.Equals(kp)).GetHttpConnection(false).Host;
                AuditorWrappers.Add(
                        host,
                        new StartupWrapper(
                            GetSettings(kp.SecretSeed, auditors),
                            Reset
                        )
                    );
            }
        }

        public void GenerateCliens(int clientsCount)
        {
            EnsureInited();
            var clients = Enumerable.Range(0, clientsCount).Select(_ => KeyPair.Random()).ToList();
            var info = AlphaWrapper.ConstellationController.Info();
            var assets = info.Assets;
            var cursor = ulong.Parse(AlphaWrapper.ProviderFactory.Provider.Cursor);
            foreach (var client in clients)
            {
                cursor++;
                foreach (var wrapper in AuditorWrappers)
                {
                    var provider = wrapper.Value.ProviderFactory.Provider;
                    var depositNotification = new DepositNotificationModel
                    {
                        Cursor = cursor.ToString(),
                        DepositTime = DateTime.UtcNow,
                        ProviderId = provider.Id,
                        Items = assets.Select(a => new DepositModel
                        {
                            Amount = 1000000000000,
                            Asset = a.Code,
                            Destination = client.PublicKey,
                            IsSuccess = true,
                            TransactionHash = new byte[] { }
                        }).ToList()
                    };
                    provider.AddDeposit(depositNotification);
                }
            }
            Clients.AddRange(clients);
        }

        NetSDK.ConstellationInfo sdkConstellationInfo;

        private bool isInited;

        public NetSDK.ConstellationInfo SDKConstellationInfo
        {
            get
            {
                EnsureInited();
                if (sdkConstellationInfo == null)
                    sdkConstellationInfo = AlphaWrapper.ConstellationController.Info().ToSdkModel();
                return sdkConstellationInfo;
            }
        }

        private Settings GetSettings(string secret, List<Settings.Auditor> auditors)
        {
            var settings = new Settings
            {
                ListeningPort = 80,
                Secret = secret,
                SyncBatchSize = 500,
                GenesisAuditors = auditors,
                ConnectionString = "",
                IsPrimeNode = true
            };
            settings.Build();
            return settings;
        }

        public void RunAuditors()
        {
            EnsureInited();

            foreach (var auditor in AuditorWrappers)
                auditor.Value.Run(AuditorWrappers);
        }

        private void EnsureInited()
        {
            if (!isInited)
                throw new InvalidOperationException("Call init first.");
        }

        public void Dispose()
        {
            foreach (var auditor in AuditorWrappers)
                auditor.Value.Dispose();
        }
    }
}
