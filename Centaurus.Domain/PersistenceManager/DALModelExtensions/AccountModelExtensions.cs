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
                Id = accountModel.Id,
                Nonce = accountModel.Nonce,
                Pubkey = new RawPubKey { Data = accountModel.PubKey },
                Withdrawal = accountModel.Withdrawal
            };

            if (accountModel.RequestRateLimits != null)
                acc.RequestRateLimits = new RequestRateLimits { HourLimit = accountModel.RequestRateLimits.HourLimit, MinuteLimit = accountModel.RequestRateLimits.MinuteLimit };

            acc.Balances = balances.Select(b => b.ToBalance()).OrderBy(a => a.Asset).ToList();
            return acc;
        }
    }
}
