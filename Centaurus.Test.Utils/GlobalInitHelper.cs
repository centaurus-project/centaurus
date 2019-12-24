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
            settings.Secret = secret;
            settings.CWD = "AppData";
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static void DefaultAlphaSetup()
        {
            DefaultSetup(GetAlphaSettings());
        }

        /// <summary>
        /// Setups Global with predefined settings
        /// </summary>
        public static void DefaultAuditorSetup()
        {
            DefaultSetup(GetAuditorSettings());
        }

        private static void DefaultSetup(BaseSettings baseSettings)
        {
            Setup(new List<KeyPair> { TestEnvironment.Client1KeyPair, TestEnvironment.Client2KeyPair }, new List<KeyPair> { TestEnvironment.Auditor1KeyPair }, baseSettings);
        }

        /// <summary>
        /// This method inits Global, generates genesis snapshot and adds clients to constellation
        /// </summary>
        /// <param name="clients">Clients to add to constellation</param>
        /// <param name="auditors">Auditors to add to constellation</param>
        /// <param name="settings">Settings that will be used to init Global</param>
        public static void Setup(List<KeyPair> clients, List<KeyPair> auditors, BaseSettings settings)
        {
            Global.Init(settings, new MockStorage());

            var assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = KeyPair.Random() } };

            var constellationSettings = new ConstellationSettings
            {
                Assets = assets,
                Auditors = auditors.Select(a => (RawPubKey)a.PublicKey).ToList(),
                MinAccountBalance = 1,
                MinAllowedLotSize = 1,
                Vault = settings is AlphaSettings ? settings.KeyPair.PublicKey : ((AuditorSettings)settings).AlphaKeyPair.PublicKey
            };

            SnapshotManager.BuildGenesisSnapshot(constellationSettings, 1, 1).Wait();

            var accountUpdates = new List<DiffObject.Account>();
            var balances = new List<DiffObject.Balance>();
            for (var i = 0; i < clients.Count; i++)
            {
                accountUpdates.Add(new DiffObject.Account { IsInserted = true, PubKey = clients[i].PublicKey });
                balances.Add(new DiffObject.Balance { IsInserted = true, PubKey = clients[i].PublicKey, Amount = 10000, AssetId = 0 });
                for (var c = 0; c < assets.Count; c++)
                {
                    balances.Add(new DiffObject.Balance { IsInserted = true, PubKey = clients[i].PublicKey, Amount = 10000, AssetId = assets[c].Id });
                }
            }

            Global.PermanentStorage.Update(new DiffObject
            {
                Accounts = accountUpdates,
                Balances = balances,
                Assets = new List<DAL.Models.AssetModel>(),
                Effects = new List<DAL.Models.EffectModel>(),
                Orders = new List<DiffObject.Order>(),
                Quanta = new List<DAL.Models.QuantumModel>(),
                Widthrawals = new List<DiffObject.Withdrawal>(),
            }).Wait();

            var snapshot = SnapshotManager.GetSnapshot().Result;

            Global.Setup(snapshot);
        }
    }
}
