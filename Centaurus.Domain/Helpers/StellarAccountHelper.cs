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
        public static async Task<AccountResponse> GetStellarAccount(this KeyPair keyPair, StellarNetwork stellarNetwork)
        {
            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));
            return await stellarNetwork.Server.Accounts.Account(keyPair.AccountId);
        }
    }
}
