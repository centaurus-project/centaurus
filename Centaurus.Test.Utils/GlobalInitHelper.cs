using Centaurus.DAL;
using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public static class GlobalInitHelper
    {
        /// <summary>
        /// Generates alpha server settings
        /// </summary>
        /// <returns>Settings object</returns>
        public static Settings GetAlphaSettings()
        {
            var settings = new Settings();
            SetCommonSettings(settings, TestEnvironment.AlphaKeyPair.SecretSeed);
            settings.ConnectionString = "mongodb://localhost:27001/alphaDBTest?replicaSet=centaurus";
            settings.Build();
            return settings;
        }


        /// <summary>
        /// Generates auditor server settings
        /// </summary>
        /// <returns>Settings object</returns>
        public static Settings GetAuditorSettings()
        {
            var settings = new Settings();
            SetCommonSettings(settings, TestEnvironment.Auditor1KeyPair.SecretSeed);
            settings.ConnectionString = "mongodb://localhost:27001/auditorDBTest?replicaSet=centaurus";
            settings.Build();
            return settings;
        }

        private static void SetCommonSettings(Settings settings, string secret)
        {
            settings.HorizonUrl = "https://horizon-testnet.stellar.org";
            settings.NetworkPassphrase = "Test SDF Network ; September 2015";
            settings.CWD = "AppData";
            settings.AlphaPubKey = TestEnvironment.AlphaKeyPair.AccountId;
            settings.AuditorAddressBook = new string[] { "http://localhost" };
            settings.Secret = secret;
            settings.ParticipationLevel = 1;
        }

        public static List<KeyPair> GetPredefinedClients()
        {
            return new List<KeyPair> { TestEnvironment.Client1KeyPair, TestEnvironment.Client2KeyPair };
        }

        public static List<KeyPair> GetPredefinedAuditors()
        {
            return new List<KeyPair> { TestEnvironment.AlphaKeyPair, TestEnvironment.Auditor1KeyPair };
        }

        private static async Task<IStorage> GetStorage(string connectionString)
        {
            var storage = new MockStorage();
            await storage.OpenConnection(connectionString);
            await storage.DropDatabase();
            await storage.CloseConnection();
            return storage;
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static async Task<ExecutionContext> DefaultAlphaSetup()
        {
            var settings = GetAlphaSettings();
            var storage = await GetStorage(settings.ConnectionString);
            return await Setup(GetPredefinedClients(), GetPredefinedAuditors(), settings, storage);
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static async Task<ExecutionContext> DefaultAuditorSetup()
        {
            var settings = GetAuditorSettings();
            var storage = await GetStorage(settings.ConnectionString);
            return await Setup(GetPredefinedClients(), GetPredefinedAuditors(), settings, storage);
        }

        /// <summary>
        /// This method inits Global, generates genesis snapshot and adds clients to constellation
        /// </summary>
        /// <param name="clients">Clients to add to constellation</param>
        /// <param name="auditors">Auditors to add to constellation</param>
        /// <param name="settings">Settings that will be used to init Global</param>
        public static async Task<ExecutionContext> Setup(List<KeyPair> clients, List<KeyPair> auditors, Settings settings, IStorage storage)
        {
            var stellarProvider = new MockStellarDataProvider(settings.NetworkPassphrase, settings.HorizonUrl);

            var context = new ExecutionContext(settings, storage, stellarProvider);

            await context.Init();

            var assets = new List<AssetSettings> { new AssetSettings { Id = 0, Code = "XLM", MinDeposit = 1 }, new AssetSettings { Id = 1, Code = "USD", MinDeposit = -1 } };

            var stellarProviderVault = KeyPair.Random().AccountId;

            var stellarProviderSettings = new ProviderSettings
            {
                Provider = "Stellar",
                Cursor = "0",
                Vault = stellarProviderVault,
                Assets = new List<ProviderAsset>
                        {
                            new ProviderAsset { CentaurusAsset = 0 },
                            new ProviderAsset { CentaurusAsset = 1, Token = $"USD-{stellarProviderVault}-1", IsVirtual = true }
                        },
                Name = "Test SDF Network ; September 2015",
                PaymentSubmitDelay = 0
            };

            var initRequest = new ConstellationInitRequest
            {
                Assets = assets,
                Auditors = auditors.Select(a => (RawPubKey)a.PublicKey).ToList(),
                MinAccountBalance = 1,
                MinAllowedLotSize = 1,
                Providers = new List<ProviderSettings> { stellarProviderSettings },
                RequestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 },
            }.CreateEnvelope()
                .Sign(TestEnvironment.AlphaKeyPair)
                .Sign(TestEnvironment.Auditor1KeyPair);

            await context.QuantumHandler.HandleAsync(new ConstellationQuantum { Apex = 1, RequestEnvelope = initRequest }.CreateEnvelope());

            var deposits = new List<Deposit>();
            Action<byte[], int> addAssetsFn = (acc, asset) =>
            {
                deposits.Add(new Deposit
                {
                    Destination = acc,
                    Amount = 10000,
                    Asset = asset,
                    PaymentResult = PaymentResults.Success
                });
            };

            for (int i = 0; i < clients.Count; i++)
            {
                //add xlm
                addAssetsFn(clients[i].PublicKey, 0);
                for (var c = 0; c < assets.Count; c++)
                    addAssetsFn(clients[i].PublicKey, assets[c].Id);
            }

            var providerId = PaymentProviderBase.GetProviderId(stellarProviderSettings.Provider, stellarProviderSettings.Name);

            var txNotification = new DepositNotification
            {
                Cursor = "2",
                Items = deposits,
                ProviderId = providerId
            };

            context.PaymentProvidersManager.TryGetManager(providerId, out var paymentProvider);
            paymentProvider.NotificationsManager.RegisterNotification(txNotification);

            var depositQuantum = new DepositQuantum
            {
                Apex = 2,
                PrevHash = context.QuantumStorage.LastQuantumHash,
                Source = txNotification,
            };

            await context.QuantumHandler.HandleAsync(depositQuantum.CreateEnvelope());

            //save all effects
            await ContextHelpers.ApplyUpdates(context);
            return context;
        }
    }
}
