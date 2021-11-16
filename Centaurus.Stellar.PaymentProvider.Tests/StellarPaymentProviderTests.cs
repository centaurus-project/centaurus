using Centaurus.PaymentProvider.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Centaurus.Stellar.PaymentProvider.Tests
{
    public class StellarPaymentProviderTests
    {
        [Test]
        public void InitTest()
        {
            var provider = new StellarPaymentProvider(new SettingsModel
            {
                Cursor = "6180462233923584",
                Name = "TestNet",
                Provider = "Stellar",
                PaymentSubmitDelay = 20,
                Vault = "GCNCAGVEMEKJKT7TDFI4S6IBQOFOF57SZE2C765NO5WCGT3ZNRDELOAX",
                Assets = new List<AssetModel>
                {
                    new AssetModel
                    {
                        CentaurusAsset = "XLM",
                        Token = "native"
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast0",
                        Token = "ast0:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast1",
                        Token = "ast1:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast2",
                        Token = "ast2:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast3",
                        Token = "ast3:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast4",
                        Token = "ast4:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast5",
                        Token = "ast5:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast6",
                        Token = "ast6:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast7",
                        Token = "ast7:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast8",
                        Token = "ast8:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast9",
                        Token = "ast9:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    }
                }
            }, JsonSerializer.Serialize(new
            {
                Horizon = "https://horizon-testnet.stellar.org",
                Secret = "SAP3AXLCYPIR4I4HJCIVO67Q52ITPI5O5FSEDZFNJNFZRMYQRCRFFZBI",
                PassPhrase = "Test SDF Network ; September 2015"
            }));
        }

        private SettingsModel GetSettings(string vault)
        {
            return new SettingsModel
            {
                Cursor = long.MaxValue.ToString(),
                Name = "TestNet",
                Provider = "Stellar",
                PaymentSubmitDelay = 20,
                Vault = vault,
                Assets = new List<AssetModel>
                {
                    new AssetModel
                    {
                        CentaurusAsset = "XLM",
                        Token = "native"
                    }
                }
            };
        }

        private List<StellarPaymentProvider> GetProviders(string vault, string serverUrl, string network, List<string> providerSecrets)
        {
            var settings = GetSettings(vault);
            var providers = new List<StellarPaymentProvider>();
            foreach (var providerSecret in providerSecrets)
            {
                var provider = new StellarPaymentProvider(settings, JsonSerializer.Serialize(new
                {
                    Horizon = serverUrl,
                    Secret = providerSecret,
                    PassPhrase = network
                }));
                providers.Add(provider);
            }
            return providers;
        }

        private async Task<(ITransactionBuilderAccount vault, List<string> signerSecrets)> SetupVault(Server server, int signersCount)
        {
            var vault = KeyPair.Random();

            var fundResponse = await server.TestNetFriendBot.FundAccount(vault.AccountId).Execute();

            Assert.IsNull(fundResponse.Status);


            var vaultAccount = await server.Accounts.Account(vault.AccountId);
            var txBuilder = new TransactionBuilder(vaultAccount);
            var signerSecrets = new List<string>();
            for (var i = 0; i < signersCount; i++)
            {
                var providerKeypair = KeyPair.Random();
                signerSecrets.Add(providerKeypair.SecretSeed);
                txBuilder.AddOperation(new SetOptionsOperation.Builder().SetSigner(providerKeypair.XdrSignerKey, 1).Build());
            }
            txBuilder.AddOperation(new SetOptionsOperation.Builder().SetMasterKeyWeight(0).Build());
            var threshold = (signersCount / 2) + 1;
            txBuilder.AddOperation(new SetOptionsOperation.Builder().SetHighThreshold(threshold).Build());
            txBuilder.AddOperation(new SetOptionsOperation.Builder().SetMediumThreshold(threshold).Build());
            var tx = txBuilder.Build();
            tx.Sign(vault);

            var submitResult = await server.SubmitTransaction(tx);

            Assert.IsTrue(submitResult.IsSuccess());

            return (vaultAccount, signerSecrets);
        }

        private List<(byte[] tx, List<SignatureModel> signatures)> BuildTransactions(List<StellarPaymentProvider> providers, byte[] destination, int withdrawalsCount)
        {
            var transactions = new List<(byte[] tx, List<SignatureModel> signatures)>();
            var alphaProvider = providers.First();
            for (var i = 0; i < withdrawalsCount; i++)
            {
                var providerTx = alphaProvider.BuildTransaction(new WithdrawalRequestModel
                {
                    Amount = 100,
                    Asset = "XLM",
                    Destination = destination,
                    PaymentProvider = alphaProvider.Id,
                    Fee = 10000
                });

                var signatures = new List<SignatureModel>();
                foreach (var p in providers)
                {
                    signatures.Add(p.SignTransaction(providerTx));
                }
                transactions.Add((providerTx, signatures));
            }
            return transactions;
        }

        private List<Task<bool>> SubmitTransactions(StellarPaymentProvider provider, List<(byte[] tx, List<SignatureModel> signatures)> transactions)
        {
            var tasks = new List<Task<bool>>();
            for (var i = transactions.Count - 1; i >= 0; i--)
            {
                var txItem = transactions[i];
                tasks.Add(provider.SubmitTransaction(txItem.tx, txItem.signatures));
            }
            return tasks;
        }

        [Test]
        [TestCase(2, false, 1)]
        [TestCase(2, true, 1)]
        [TestCase(2, true, 5)]
        [TestCase(5, true, 2)]
        [Explicit]
        public async Task WithdrawalTest(int signersCount, bool createDest, int withdrawalsCount)
        {
            var network = new Network(Network.TestnetPassphrase);
            Network.UseTestNetwork();

            var serverUrl = "https://horizon-testnet.stellar.org";
            using var server = new Server(serverUrl);

            var vaultData = await SetupVault(server, signersCount);

            var providers = GetProviders(vaultData.vault.AccountId, serverUrl, network.NetworkPassphrase, vaultData.signerSecrets);

            var destination = KeyPair.Random();
            if (createDest)
            {
                var fundResponse = await server.TestNetFriendBot.FundAccount(destination.AccountId).Execute();
                Assert.IsNull(fundResponse.Status);
            }

            var alphaProvider = providers.First();
            var transactions = BuildTransactions(providers, destination.PublicKey, withdrawalsCount);
            var submissionTasks = SubmitTransactions(alphaProvider, transactions);
            await Task.WhenAll(submissionTasks);

            Assert.IsTrue(submissionTasks.All(t => t.Result == createDest));

            var expectedSequence = vaultData.vault.SequenceNumber + transactions.Count;
            var currentSequence = (await server.Accounts.Account(vaultData.vault.AccountId)).SequenceNumber;
            Assert.AreEqual(expectedSequence, currentSequence);

            var betaProvider = providers.Last();
            submissionTasks = SubmitTransactions(betaProvider, transactions);
            Assert.IsTrue(submissionTasks.All(t => t.Result == createDest));

            currentSequence = (await server.Accounts.Account(vaultData.vault.AccountId)).SequenceNumber;
            Assert.AreEqual(expectedSequence, currentSequence);

            //restart
            providers = GetProviders(vaultData.vault.AccountId, serverUrl, network.NetworkPassphrase, vaultData.signerSecrets);
            alphaProvider = providers.First();
            transactions = BuildTransactions(providers, destination.PublicKey, 1);
            submissionTasks = SubmitTransactions(alphaProvider, transactions);
            await Task.WhenAll(submissionTasks);

            expectedSequence++;
            currentSequence = (await server.Accounts.Account(vaultData.vault.AccountId)).SequenceNumber;
            Assert.AreEqual(expectedSequence, currentSequence);
        }
    }
}
