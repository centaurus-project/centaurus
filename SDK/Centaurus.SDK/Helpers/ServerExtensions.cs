using stellar_dotnet_sdk;
using stellar_dotnet_sdk.requests;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.SDK
{
    public static class ServerExtensions
    {
        public static async Task<AccountResponse> GetAccountData(this Server server, string account)
        {
            try
            {
                return await server.Accounts.Account(account);
            }
            catch (HttpResponseException exc)
            {
                if (exc.StatusCode == 404)
                    return null;
                throw;
            }
        }
    }
}
