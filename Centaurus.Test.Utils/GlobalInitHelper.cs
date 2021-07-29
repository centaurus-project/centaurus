using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using Centaurus.PersistentStorage.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
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
            settings.CWD = "AppData";
            settings.AlphaPubKey = TestEnvironment.AlphaKeyPair.AccountId;
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

        private static IPersistentStorage GetStorage(string connectionString)
        {
            var storage = new MockStorage();
            storage.Connect(connectionString);
            return storage;
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static async Task<ExecutionContext> DefaultAlphaSetup()
        {
            var settings = GetAlphaSettings();
            var storage = GetStorage(settings.ConnectionString);
            return await Setup(GetPredefinedClients(), GetPredefinedAuditors(), settings, storage);
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static async Task<ExecutionContext> DefaultAuditorSetup()
        {
            var settings = GetAuditorSettings();
            var storage = GetStorage(settings.ConnectionString);
            return await Setup(GetPredefinedClients(), GetPredefinedAuditors(), settings, storage);
        }

        /// <summary>
        /// This method inits Global, generates genesis snapshot and adds clients to constellation
        /// </summary>
        /// <param name="clients">Clients to add to constellation</param>
        /// <param name="auditors">Auditors to add to constellation</param>
        /// <param name="settings">Settings that will be used to init Global</param>
        public static async Task<ExecutionContext> Setup(List<KeyPair> clients, List<KeyPair> auditors, Settings settings, IPersistentStorage storage)
        {
            var context = new ExecutionContext(settings, storage, new MockPaymentProviderFactory(), new MockOutgoingConnectionFactory());

            var assets = new List<AssetSettings> { new AssetSettings { Code = "XLM" }, new AssetSettings { Code = "USD" } };

            var stellarProviderVault = KeyPair.Random().AccountId;

            var stellarProviderSettings = new ProviderSettings
            {
                Provider = "Stellar",
                InitCursor = "0",
                Vault = stellarProviderVault,
                Assets = new List<ProviderAsset>
                        {
                            new ProviderAsset { CentaurusAsset = "XLM", Token = "native" },
                            new ProviderAsset { CentaurusAsset = "USD", Token = $"USD-{stellarProviderVault}", IsVirtual = true }
                        },
                Name = "Test SDF Network ; September 2015",
                PaymentSubmitDelay = 0
            };

            var initRequest = new ConstellationUpdate
            {
                Assets = assets,
                Auditors = auditors.Select(a => new Auditor { PubKey = a.PublicKey, Address = "" }).ToList(),
                MinAccountBalance = 1,
                MinAllowedLotSize = 1,
                Providers = new List<ProviderSettings> { stellarProviderSettings },
                RequestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 },
            }.CreateEnvelope()
                .Sign(TestEnvironment.AlphaKeyPair)
                .Sign(TestEnvironment.Auditor1KeyPair);

            await context.QuantumHandler.HandleAsync(new ConstellationQuantum { Apex = 1, RequestEnvelope = initRequest }.CreateEnvelope());

            var deposits = new List<DepositModel>();
            Action<byte[], string> addAssetsFn = (acc, asset) =>
            {
                deposits.Add(new DepositModel
                {
                    Destination = acc,
                    Amount = 10000,
                    Asset = asset,
                    IsSuccess = true
                });
            };

            for (int i = 0; i < clients.Count; i++)
            {
                var acc = context.AccountStorage.CreateAccount(context.AccountStorage.NextAccountId, clients[i].PublicKey, context.Constellation.RequestRateLimits);
                for (var c = 0; c < assets.Count; c++)
                    addAssetsFn(acc.Account.Pubkey, assets[c].Code);
            }

            var providerId = PaymentProviderBase.GetProviderId(stellarProviderSettings.Provider, stellarProviderSettings.Name);

            var txNotification = new DepositNotificationModel
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
                Source = txNotification.ToDomainModel()
            };

            await context.QuantumHandler.HandleAsync(depositQuantum.CreateEnvelope());

            //save all effects
            context.PendingUpdatesManager.UpdateBatch(true);
            return context;
        }
    }
}
