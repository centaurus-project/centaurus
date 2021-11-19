using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using Centaurus.PersistentStorage.Abstraction;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            settings.GenesisAuditors = new[] { TestEnvironment.AlphaKeyPair.AccountId, TestEnvironment.Auditor1KeyPair.AccountId }.Select(a => new Settings.Auditor($"{a}={a}.com")).ToList();
            settings.Secret = secret;
            settings.ParticipationLevel = 1;
            settings.SyncBatchSize = 500;
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
        public static async Task<ExecutionContext> DefaultAlphaSetup(IPersistentStorage storage = null, Settings settings = null)
        {
            settings = settings ?? GetAlphaSettings();
            storage = storage ?? GetStorage(settings.ConnectionString);
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
            var context = new ExecutionContext(settings, storage, new MockPaymentProviderFactory(), new DummyConnectionWrapperFactory());


            var assets = new List<AssetSettings> { new AssetSettings { Code = "XLM", IsQuoteAsset = true }, new AssetSettings { Code = "USD" } };

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
                Alpha = auditors.First().PublicKey,
                Assets = assets,
                Auditors = auditors.Select(a => new Auditor { PubKey = a.PublicKey, Address = $"{a.Address}.com" }).ToList(),
                MinAccountBalance = 1,
                MinAllowedLotSize = 1,
                Providers = new List<ProviderSettings> { stellarProviderSettings },
                RequestRateLimits = new RequestRateLimits { HourLimit = uint.MaxValue, MinuteLimit = uint.MaxValue },
            }.CreateEnvelope<ConstellationMessageEnvelope>()
                .Sign(TestEnvironment.AlphaKeyPair)
                .Sign(TestEnvironment.Auditor1KeyPair);

            await context.QuantumHandler.HandleAsync(new ConstellationQuantum { RequestEnvelope = initRequest }, Task.FromResult(true)).OnProcessed;

            var deposits = new List<DepositModel>();
            Action<byte[], string> addAssetsFn = (acc, asset) =>
            {
                deposits.Add(new DepositModel
                {
                    Destination = acc,
                    Amount = ulong.MaxValue / 2,
                    Asset = asset,
                    IsSuccess = true
                });
            };

            foreach (var client in clients)
            {
                for (var c = 0; c < assets.Count; c++)
                    addAssetsFn(client.PublicKey, assets[c].Code);
            }

            var providerId = PaymentProviderBase.GetProviderId(stellarProviderSettings.Provider, stellarProviderSettings.Name);

            var txNotification = new DepositNotificationModel
            {
                Cursor = "2",
                Items = deposits,
                ProviderId = providerId
            };

            var paymentProvider = (MockPaymentProvider)context.PaymentProvidersManager.GetManager(providerId);
            paymentProvider.AddDeposit(txNotification);

            var depositQuantum = new DepositQuantum
            {
                Apex = context.QuantumHandler.CurrentApex + 1,
                PrevHash = context.QuantumHandler.LastQuantumHash,
                Source = txNotification.ToDomainModel()
            };

            await context.QuantumHandler.HandleAsync(depositQuantum, Task.FromResult(true)).OnProcessed;

            //save all effects
            await Task.Factory.StartNew(() =>
            {
                context.PendingUpdatesManager.UpdateBatch(true);
                context.PendingUpdatesManager.ApplyUpdates();
            });
            return context;
        }

        public static void SetState(this ExecutionContext context, State state)
        {
            var method = typeof(StateManager).GetMethod("SetState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(context.StateManager, new object[] { state, null });
        }
    }
}
