using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class AccountModelExtensions
    {
        public static Account ToAccount(this DAL.Models.AccountModel accountModel, BalanceModel[] balances)
        {
            var acc = new Account
            {
                Nonce = unchecked((ulong)accountModel.Nonce),
                Pubkey = new RawPubKey { Data = accountModel.PubKey }
            };

            if (accountModel.RequestRateLimits != null)
                acc.RequestRateLimits = new RequestRateLimits { HourLimit = accountModel.RequestRateLimits.HourLimit, MinuteLimit = accountModel.RequestRateLimits.MinuteLimit };

            acc.Balances = balances.Select(b => b.ToBalance(acc)).ToList();
            return acc;
        }
    }
}
