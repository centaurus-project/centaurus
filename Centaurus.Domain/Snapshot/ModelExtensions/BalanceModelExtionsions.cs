using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class BalanceModelExtionsions
    {
        public static BalanceModel[] FromAccount(Account account)
        {
            var pubkey = account.Pubkey.Data;
            return account.Balances.Select(b => FromBalance(b)).ToArray();
        }

        public static BalanceModel FromBalance(Balance balance)
        {
            return new BalanceModel
            {
                Account = balance.Account.Pubkey.Data,
                Amount = balance.Amount,
                AssetId = balance.Asset,
                Liabilities = balance.Liabilities
            };
        }

        public static Balance ToBalance(this BalanceModel balance, Account account)
        {
            return new Balance
            {
                Account = account,
                Asset = balance.AssetId,
                Amount = balance.Amount,
                Liabilities = balance.Liabilities
            };
        }
    }
}
