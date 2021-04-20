using Centaurus.Controllers;
using Centaurus.DAL.Models;
using Centaurus.Domain;
using Centaurus.Stellar;
using Microsoft.AspNetCore.Mvc;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class IntegrationTestEnvironment
    {
        public IntegrationTestEnvironment()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();

            StellarProvider = new MockStellarDataProvider(NetworkPassphrase, HorizonUrl);
            Reset = new ManualResetEvent(false);
        }

        public const string HorizonUrl = "https://horizon-testnet.stellar.org";
        public const string NetworkPassphrase = "Test SDF Network ; September 2015";
        public const string AlphaAddress = "wss://localhost";
        public KeyPair Issuer { get; } = KeyPair.Random();

        public MockStellarDataProvider StellarProvider { get; }
        public ManualResetEvent Reset { get; }
        public AlphaStartup AlphaStartup { get; private set; }
        public ConstellationController ConstellationController { get; private set; }
        public List<AuditorStartup> AuditorStartups { get; } = new List<AuditorStartup>();
        public List<KeyPair> Clients { get; } = new List<KeyPair>();

        public async Task Init(int auditorsCount)
        {
            await RunAlpha();
            await RunAuditors(auditorsCount);
        }

        public void GenerateCliens(int clientsCount)
        {
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

        public MockClientConnectionWrapper GetClientConnectionWrapper()
        {
            var serverSocket = new MockWebSocket();
            var auditorSocket = new MockWebSocket();

            return new MockClientConnectionWrapper(AlphaStartup.Context, auditorSocket, serverSocket);
        }

        SDK.Models.ConstellationInfo sdkConstellationInfo;
        public SDK.Models.ConstellationInfo SDKConstellationInfo
        {
            get
            {
                if (sdkConstellationInfo == null)
                    sdkConstellationInfo = ConstellationController.Info().ToSdkModel();
                return sdkConstellationInfo;
            }
        }

        private AlphaSettings GetAlphaSettings()
        {
            var alphaSettings = new AlphaSettings
            {
                HorizonUrl = HorizonUrl,
                NetworkPassphrase = NetworkPassphrase,
                Secret = KeyPair.Random().SecretSeed,
                SyncBatchSize = 500
            };
            alphaSettings.Build();
            return alphaSettings;
        }

        private async Task RunAlpha()
        {
            var settings = GetAlphaSettings();

            RegisterStellarAccount(settings.KeyPair);

            var alphaContext = new AlphaContext(settings, new MockStorage(), StellarProvider);
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

        private AuditorSettings GetAuditorSettings(KeyPair keyPair, IEnumerable<string> genesisQuorum)
        {
            var settings = new AuditorSettings
            {
                AlphaPubKey = AlphaStartup.Context.Settings.KeyPair.AccountId,
                Secret = KeyPair.Random().SecretSeed,
                NetworkPassphrase = NetworkPassphrase,
                HorizonUrl = HorizonUrl,
                GenesisQuorum = genesisQuorum,
                AlphaAddress = AlphaAddress
            };
            settings.Build();
            return settings;
        }

        private async Task<AuditorStartup> RunAuditor(KeyPair keyPair, IEnumerable<string> genesisQuorum)
        {
            var settings = GetAuditorSettings(keyPair, genesisQuorum);

            var auditorContext = new AuditorContext(settings, new MockStorage(), StellarProvider);

            var auditorStartup = new AuditorStartup(auditorContext, GetClientConnectionWrapper);

            await auditorStartup.Run(Reset);

            return auditorStartup;
        }

        private async Task RunAuditors(int count)
        {
            var keyPairs = Enumerable.Range(0, count).Select(_ => KeyPair.Random());
            var genesisQuorum = keyPairs.Select(k => k.AccountId);
            foreach (var keyPair in keyPairs)
                AuditorStartups.Add(await RunAuditor(keyPair, genesisQuorum));
        }
    }
}
