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
        public static Account ToAccount(this AccountModel accountModel, BalanceModel[] balances)
        {
            var acc = new Account
            {
                Nonce = accountModel.Nonce,
                Pubkey = new RawPubKey { Data = accountModel.PubKey }
            };

            acc.Balances = balances.Select(b => b.ToBalance(acc)).ToList();
            return acc;
        }
    }
}
