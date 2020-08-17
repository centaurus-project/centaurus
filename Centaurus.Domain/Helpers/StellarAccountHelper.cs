using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class StellarAccountHelper
    {
        public static async Task<AccountResponse> GetStellarAccount(this KeyPair keyPair)
        {
            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));
            return await Global.StellarNetwork.Server.Accounts.Account(keyPair.AccountId);
        }

        public static async Task<AccountResponse> GetVaultStellarAccount()
        {
            return await Global.VaultAccount.GetStellarAccount();
        }


        public static async Task<AccountResponse> GetStellarAccount(this AccountData accountData)
        {
            if (accountData == null)
                throw new ArgumentNullException(nameof(accountData));
            return await accountData.KeyPair.GetStellarAccount();
        }
    }
}
