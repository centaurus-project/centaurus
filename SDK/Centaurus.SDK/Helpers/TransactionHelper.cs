using Centaurus.SDK.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.SDK
{
    public static class TransactionHelper
    {
        public static async Task<TransactionBuilder> GetTxBuilder(this Server server, KeyPair sourceAccount)
        {
            var account = await server.GetAccountData(sourceAccount.AccountId);
            if (account == null)
                throw new Exception($"{sourceAccount.AccountId} is not a part of Stellar Network.");

            var txBuilder = new TransactionBuilder(account);
            txBuilder.SetFee(10_000);
            return txBuilder;
        }

        public static async Task<Transaction> GetWithdrawalTx(KeyPair sourceAccount, ConstellationInfo constellation, KeyPair destination, string amount, ConstellationInfo.Asset asset)
        {
            using (var server = constellation.StellarNetwork.GetServer())
            {
                var txBuilder = await GetTxBuilder(server, sourceAccount);

                if ((await server.GetAccountData(destination.AccountId)) == null)
                    throw new Exception($"{destination.AccountId} is not a part of Stellar Network.");

                txBuilder.AddOperation(new PaymentOperation.Builder(destination, asset.StellarAsset, amount).SetSourceAccount(KeyPair.FromAccountId(constellation.Vault)).Build());
                txBuilder.AddTimeBounds(new stellar_dotnet_sdk.TimeBounds(maxTime: DateTimeOffset.UtcNow.AddSeconds(60)));
                var tx = txBuilder.Build();
                return tx;
            }
        }

        public static async Task<Transaction> GetDepositTx(KeyPair sourceAccount, ConstellationInfo constellation, string amount, ConstellationInfo.Asset asset)
        {
            using (var server = constellation.StellarNetwork.GetServer())
            {
                var txBuilder = await GetTxBuilder(server, sourceAccount);

                txBuilder.AddOperation(new PaymentOperation.Builder((KeyPair)constellation.VaultPubKey, asset.StellarAsset, amount).Build());
                var tx = txBuilder.Build();
                return tx;
            }
        }
    }
}
