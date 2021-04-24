using Centaurus.Controllers;
using Centaurus.DAL.Models;
using Centaurus.Domain;
using Centaurus.Stellar;
using Microsoft.AspNetCore.Mvc;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class IntegrationTestEnvironment: IDisposable
    {
        public IntegrationTestEnvironment()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();

            StellarProvider = new MockStellarDataProvider(NetworkPassphrase, HorizonUrl);
            Reset = new ManualResetEvent(false);
        }

        public const string HorizonUrl = "https://horizon-testnet.stellar.org";
        public const string NetworkPassphrase = "Test SDF Network ; September 2015";
        public const int AlphaPort = 5000;

        public static string AlphaAddress { get; } = $"ws://localhost:{AlphaPort}/centaurus";

        public KeyPair Issuer { get; } = KeyPair.Random();

        public MockStellarDataProvider StellarProvider { get; }
        public ManualResetEvent Reset { get; }
        public AlphaStartup AlphaStartup { get; private set; }
        public ConstellationController ConstellationController { get; private set; }
        public List<AuditorStartup> AuditorStartups { get; } = new List<AuditorStartup>();
        public List<KeyPair> Clients { get; } = new List<KeyPair>();

        public void Init(int auditorsCount)
        {
            if (isInited)
                throw new InvalidOperationException("Already inited");
            GenerateAlpha();
            GenerateAuditors(auditorsCount);
            isInited = true;
        }

        private void GenerateAlpha()
        {
            alphaSettings = GetAlphaSettings();
            RegisterStellarAccount(alphaSettings.KeyPair);
        }

        private void GenerateAuditors(int auditorsCount)
        {
            var keyPairs = Enumerable.Range(0, auditorsCount).Select(_ => KeyPair.Random()).ToArray();
            GenesisQuorum = keyPairs.Select(k => k.AccountId);
            auditorSettings = keyPairs.Select(kp => GetAuditorSettings(kp)).ToList();
        }

        public void GenerateCliens(int clientsCount)
        {
            EnsureInited();
            var clients = Enumerable.Range(0, clientsCount).Select(_ => KeyPair.Random()).ToList();
            var info = ConstellationController.Info();
            var assets = info.Assets;
            var vault = KeyPair.FromAccountId(info.Vault);
            foreach (var client in clients)
            {
                var accountModel = RegisterStellarAccount(client);

                var transaction = new TransactionBuilder(accountModel.ToITransactionBuilderAccount());
                foreach (var asset in assets)
                {
                    var stellarAsset = asset.Issuer == null ? new AssetTypeNative() : Asset.CreateNonNativeAsset(asset.Code, asset.Issuer);
                    transaction.AddOperation(new PaymentOperation.Builder(vault, stellarAsset, 1000.ToString()).Build());
                }

                StellarProvider.SubmitTransaction(transaction.Build());
            }
            Clients.AddRange(clients);
        }

        public ClientConnectionWrapperBase GetClientConnectionWrapper()
        {
            var clientConnection = new MockWebSocket();
            var serverConnection = new MockWebSocket();
            return new MockClientConnectionWrapper(AlphaStartup.Context, clientConnection, serverConnection);

            //return new ClientConnectionWrapper(new ClientWebSocket());
        }

        SDK.Models.ConstellationInfo sdkConstellationInfo;
        private AlphaSettings alphaSettings;

        public IEnumerable<string> GenesisQuorum { get; private set; }

        private List<AuditorSettings> auditorSettings;
        private bool isInited;

        public SDK.Models.ConstellationInfo SDKConstellationInfo
        {
            get
            {
                EnsureInited();
                if (sdkConstellationInfo == null)
                    sdkConstellationInfo = ConstellationController.Info().ToSdkModel();
                return sdkConstellationInfo;
            }
        }

        private AlphaSettings GetAlphaSettings()
        {
            var alphaSettings = new AlphaSettings
            {
                AlphaPort = 5000,
                HorizonUrl = HorizonUrl,
                NetworkPassphrase = NetworkPassphrase,
                Secret = KeyPair.Random().SecretSeed,
                SyncBatchSize = 500
            };
            alphaSettings.Build();
            return alphaSettings;
        }

        public async Task RunAlpha()
        {
            EnsureInited();
            var alphaContext = new AlphaContext(alphaSettings, new MockStorage(), StellarProvider);
            AlphaStartup = new AlphaStartup(alphaContext);
            ConstellationController = new ConstellationController(alphaContext);
            await AlphaStartup.Run(Reset);
        }

        private Stellar.Models.AccountModel RegisterStellarAccount(KeyPair keyPair)
        {
            var accountModel = new Stellar.Models.AccountModel
            {
                KeyPair = keyPair,
                SequenceNumber = 1,
                ExistingTrustLines = new List<string>()
            };
            StellarProvider.RegisterAccount(accountModel);
            return accountModel;
        }

        private AuditorSettings GetAuditorSettings(KeyPair keyPair)
        {
            var settings = new AuditorSettings
            {
                AlphaPubKey = alphaSettings.KeyPair.AccountId,
                Secret = keyPair.SecretSeed,
                NetworkPassphrase = NetworkPassphrase,
                HorizonUrl = HorizonUrl,
                GenesisQuorum = GenesisQuorum,
                AlphaAddress = AlphaAddress
            };
            settings.Build();
            return settings;
        }

        private async Task<AuditorStartup> RunAuditor(AuditorSettings settings)
        {
            var auditorContext = new AuditorContext(settings, new MockStorage(), StellarProvider);

            var auditorStartup = new AuditorStartup(auditorContext, GetClientConnectionWrapper);

            await auditorStartup.Run(Reset);

            return auditorStartup;
        }

        public async Task RunAuditors()
        {
            EnsureInited();
            foreach (var settings in auditorSettings)
                AuditorStartups.Add(await RunAuditor(settings));
        }

        private void EnsureInited()
        {
            if (!isInited)
                throw new InvalidOperationException("Call init first.");
        }

        public void Dispose()
        {
            AlphaStartup.Shutdown().Wait();
            foreach (var auditor in AuditorStartups)
            {
                auditor.Shutdown().Wait();
            }
        }
    }
}
