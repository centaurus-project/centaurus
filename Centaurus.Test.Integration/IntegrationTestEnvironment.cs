using Centaurus.Controllers;
using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
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
    public class IntegrationTestEnvironment : IDisposable
    {
        public IntegrationTestEnvironment()
        {
            StellarProvider = new MockStellarDataProvider(NetworkPassphrase, HorizonUrl);
            Reset = new ManualResetEvent(false);
        }

        public const string HorizonUrl = "https://horizon-testnet.stellar.org";
        public const string NetworkPassphrase = "Test SDF Network ; September 2015";
        public const int AlphaPort = 5000;

        public static string AlphaAddress { get; } = $"ws://localhost:{AlphaPort}/centaurus";

        public KeyPair Issuer { get; } = KeyPair.Random();

        public Vault Vault { get; } = new Vault { AccountId = KeyPair.Random().AccountId, Provider = PaymentProvider.Stellar };

        public MockStellarDataProvider StellarProvider { get; }
        public ManualResetEvent Reset { get; }

        public AlphaStartupWrapper AlphaWrapper { get; private set; }

        public List<AuditorStartupWrapper> AuditorWrappers { get; private set; } = new List<AuditorStartupWrapper>();
        public List<KeyPair> Clients { get; } = new List<KeyPair>();

        public void Init(int auditorsCount)
        {
            if (isInited)
                throw new InvalidOperationException("Already inited");

            var keyPairs = Enumerable.Range(0, auditorsCount).Select(_ => KeyPair.Random()).ToArray();
            var genesisQuorum = keyPairs.Select(k => k.AccountId).ToArray();

            GenerateAuditors(keyPairs, genesisQuorum);
            isInited = true;
        }

        private void GenerateAuditors(KeyPair[] keyPairs, string[] genesisQuorum)
        {
            foreach (var kp in keyPairs)
            {
                if (AlphaWrapper == null)
                {
                    var alphaSettings = GetSettings(kp, genesisQuorum, kp);
                    RegisterStellarAccount(alphaSettings.KeyPair);
                    AlphaWrapper = new AlphaStartupWrapper(alphaSettings, StellarProvider, Reset);
                }
                else
                {
                    AuditorWrappers.Add(new AuditorStartupWrapper(GetSettings(kp, genesisQuorum, keyPairs.First()), StellarProvider, Reset, GetClientConnectionWrapper));
                }
            }
        }

        public void GenerateCliens(int clientsCount)
        {
            EnsureInited();
            var clients = Enumerable.Range(0, clientsCount).Select(_ => KeyPair.Random()).ToList();
            var info = AlphaWrapper.ConstellationController.Info();
            var assets = info.Assets;
            info.Vaults.TryGetValue(PaymentProvider.Stellar.ToString(), out var rawVault);
            var vault = KeyPair.FromAccountId(rawVault);
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
            return new MockClientConnectionWrapper(AlphaWrapper.Startup?.Context, clientConnection, serverConnection);
        }

        SDK.Models.ConstellationInfo sdkConstellationInfo;

        private bool isInited;

        public SDK.Models.ConstellationInfo SDKConstellationInfo
        {
            get
            {
                EnsureInited();
                if (sdkConstellationInfo == null)
                    sdkConstellationInfo = AlphaWrapper.ConstellationController.Info().ToSdkModel();
                return sdkConstellationInfo;
            }
        }

        private Settings GetSettings(KeyPair keyPair, string[] genesisQuorum, KeyPair alpha)
        {
            var settings = new Settings
            {
                AlphaPort = 5000,
                HorizonUrl = HorizonUrl,
                NetworkPassphrase = NetworkPassphrase,
                Secret = keyPair.SecretSeed,
                SyncBatchSize = 500,
                AlphaPubKey = alpha.AccountId,
                AuditorAddressBook = new string[] { "http://localhost" },
                ParticipationLevel = 1
            };
            settings.Build();
            return settings;
        }

        public async Task RunAlpha()
        {
            EnsureInited();
            await AlphaWrapper.Run();
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

        public async Task RunAuditors()
        {
            EnsureInited();
            foreach (var auditor in AuditorWrappers)
                await auditor.Run();
        }

        private void EnsureInited()
        {
            if (!isInited)
                throw new InvalidOperationException("Call init first.");
        }

        public void Dispose()
        {
            AlphaWrapper.Dispose();
            foreach (var auditor in AuditorWrappers)
                auditor.Dispose();
        }
    }
}
