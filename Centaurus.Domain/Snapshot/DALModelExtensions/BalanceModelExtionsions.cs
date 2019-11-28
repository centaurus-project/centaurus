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
