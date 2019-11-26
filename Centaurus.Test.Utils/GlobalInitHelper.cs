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
            Global.Init(settings, new MongoStorage());

            var clientAccounts = GenerateClientAccounts(clients);

            var snapshot = GenerateSnapshot(clientAccounts, auditors);

            Global.Setup(snapshot);
        }

        private static List<Models.Account> GenerateClientAccounts(List<KeyPair> clients)
        {
            var accounts = new List<Models.Account>();
            foreach (var client in clients)
            {
                var account = new Models.Account
                {
                    Pubkey = client
                };


                account.Balances = new List<Balance>
                    {
                        new Balance { Amount = 10_000_000, Asset = 0, Account = account },
                        new Balance { Amount = 10_000_000, Asset = 1, Account = account },
                    };
                accounts.Add(account);
            }
            return accounts;
        }

        private static Snapshot GenerateSnapshot(List<Models.Account> accounts, List<KeyPair> auditorKeyPairs)
        {
            var snapshot = new Snapshot
            {
                Accounts = accounts,
                Apex = 0,
                Ledger = 1,
                Orders = new List<Order>(),
                Withdrawals = new List<RequestQuantum>(),
                VaultSequence = 1,
                Settings = new ConstellationSettings
                {
                    Vault = KeyPair.Random().PublicKey,
                    Auditors = auditorKeyPairs.Select(kp => (RawPubKey)kp).ToList(),
                    Assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = new RawPubKey() } }
                }
            };

            //var snapshotEnvelope = new SnapshotQuantum { Hash = snapshot.ComputeHash() }.CreateEnvelope();
            //var confEnvelope = snapshotEnvelope.CreateResult(ResultStatusCodes.Success).CreateEnvelope();
            //foreach (var auditorKeyPair in auditorKeyPairs)
            //    confEnvelope.Sign(auditorKeyPair);
            //snapshot.Confirmation = confEnvelope;
            return snapshot;
        }
    }
}
