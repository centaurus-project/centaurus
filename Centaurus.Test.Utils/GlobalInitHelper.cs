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
        public static void Setup(string[] clientSecrets, string[] auditorSecrets, BaseSettings settings)
        {
            _ = new HandshakeDataSerializer();

            Global.Init(settings);

            var auditorKeyPairs = auditorSecrets.Select(s => KeyPair.FromSecretSeed(s)).ToList();

            var clientAccounts = GenerateClientAccounts(clientSecrets);

            var snapshot = GenerateSnapshot(clientAccounts, auditorKeyPairs);

            Global.Setup(snapshot);
        }

        private static List<Models.Account> GenerateClientAccounts(string[] clientSecrets)
        {
            var accounts = new List<Models.Account>();
            foreach (var clientSecret in clientSecrets)
            {
                var account = new Models.Account()
                {
                    Pubkey = KeyPair.FromSecretSeed(clientSecret),
                    Balances = new List<Balance>
                    {
                        new Balance { Amount = 10_000_000, Asset = 0 },
                        new Balance { Amount = 10_000_000, Asset = 1 },
                    }
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
                Id = 1,
                Ledger = 1,
                Orders = new List<Order>(),
                Withdrawals = new List<PaymentRequestBase>(),
                VaultSequence = 1,
                Settings = new ConstellationSettings
                {
                    Vault = KeyPair.Random().PublicKey,
                    Auditors = auditorKeyPairs.Select(kp => (RawPubKey)kp).ToList(),
                    Assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = new RawPubKey() } }
                }
            };

            var snapshotEnvelope = new SnapshotQuantum { Hash = snapshot.ComputeHash() }.CreateEnvelope();
            var confEnvelope = snapshotEnvelope.CreateResult(ResultStatusCodes.Success).CreateEnvelope();
            foreach (var auditorKeyPair in auditorKeyPairs)
                confEnvelope.Sign(auditorKeyPair);
            snapshot.Confirmation = confEnvelope;
            return snapshot;
        }
    }
}
