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
        public static AlphaSettings GetAlphaSettings()
        {
            var settings = new AlphaSettings();
            SetCommonSettings(settings, TestEnvironment.AlphaKeyPair.SecretSeed);
            settings.Build();
            return settings;
        }


        /// <summary>
        /// Generates auditor server settings
        /// </summary>
        /// <returns>Settings object</returns>
        public static AuditorSettings GetAuditorSettings()
        {
            var settings = new AuditorSettings();
            SetCommonSettings(settings, TestEnvironment.Auditor1KeyPair.SecretSeed);
            settings.AlphaPubKey = TestEnvironment.AlphaKeyPair.AccountId;
            settings.GenesisQuorum = new string[] { TestEnvironment.Auditor1KeyPair.AccountId };
            settings.Build();
            return settings;
        }

        private static void SetCommonSettings(BaseSettings settings, string secret)
        {
            settings.HorizonUrl = "https://horizon-testnet.stellar.org";
            settings.NetworkPassphrase = "Test SDF Network ; September 2015";
            settings.CWD = "AppData";
            settings.Secret = secret;
        }

        public static List<KeyPair> GetPredefinedClients()
        {
            return new List<KeyPair> { TestEnvironment.Client1KeyPair, TestEnvironment.Client2KeyPair };
        }

        public static List<KeyPair> GetPredefinedAuditors()
        {
            return new List<KeyPair> { TestEnvironment.Auditor1KeyPair };
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static void DefaultAlphaSetup()
        {
            Setup(GetPredefinedClients(), GetPredefinedAuditors(), GetAlphaSettings(), new MockStorage());
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static void DefaultAuditorSetup()
        {
            Setup(GetPredefinedClients(), GetPredefinedAuditors(), GetAuditorSettings(), new MockStorage());
        }

        /// <summary>
        /// This method inits Global, generates genesis snapshot and adds clients to constellation
        /// </summary>
        /// <param name="clients">Clients to add to constellation</param>
        /// <param name="auditors">Auditors to add to constellation</param>
        /// <param name="settings">Settings that will be used to init Global</param>
        public static void Setup(List<KeyPair> clients, List<KeyPair> auditors, BaseSettings settings, BaseStorage storage)
        {
            Global.Init(settings, storage);

            var assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = KeyPair.Random() } };

            var initQuantum = new ConstellationInitQuantum
            {
                Apex = 1,
                Assets = assets,
                Auditors = auditors.Select(a => (RawPubKey)a.PublicKey).ToList(),
                MinAccountBalance = 1,
                MinAllowedLotSize = 1,
                Vault = settings is AlphaSettings ? settings.KeyPair.PublicKey : ((AuditorSettings)settings).AlphaKeyPair.PublicKey,
                PrevHash = new byte[] { },
                RequestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 }
            };

            var envelope = initQuantum.CreateEnvelope();
            if (!Global.IsAlpha)
            {
                var alphaKeyPair = KeyPair.FromSecretSeed(TestEnvironment.AlphaKeyPair.SecretSeed);
                envelope.Sign(alphaKeyPair);
            }

            Global.QuantumHandler.HandleAsync(envelope).Wait();


            var deposits = new List<PaymentBase>();
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

            var depositeQuantum = new TxCommitQuantum
            {
                Apex = 2,
                PrevHash = Global.QuantumStorage.LastQuantumHash,
                Source = new TxNotification
                {
                    TxCursor = 2,
                    Payments = deposits
                }.CreateEnvelope()
            };

            depositeQuantum.Source.Sign(TestEnvironment.Auditor1KeyPair);

            Global.QuantumHandler.HandleAsync(depositeQuantum.CreateEnvelope()).Wait();


            //save all effects
            SnapshotHelper.ApplyUpdates().Wait();
        }
    }
}
