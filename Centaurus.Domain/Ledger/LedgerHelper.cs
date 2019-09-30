using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class LedgerHelper
    {
        public static async Task<int> GetFirstProcessedLedger()
        {
            var kp = KeyPair.FromPublicKey(Global.Constellation.Vault.ToArray());
            var vaultTransactions = await Global.StellarNetwork.Server.Transactions.ForAccount(kp.AccountId).Limit(1).Execute();

            var createAccountTransaction = vaultTransactions.Records.FirstOrDefault();

            if (createAccountTransaction == null)
                throw new Exception("Unable to find a single transaction for the vault account");

            return (int)createAccountTransaction.Ledger - 1;
        }
    }
}
