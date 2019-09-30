using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AccountExtensions
    {
        /// <summary>
        /// Retrieve account balance.
        /// </summary>
        /// <param name="account">Account record</param>
        /// <param name="asset">Asset id</param>
        /// <param name="createIfNotExist">Set true only on deposit</param>
        /// <returns></returns>
        public static Balance GetBalance(this Account account, int asset, bool createIfNotExist = false)
        {
            var balance = account.Balances.Find(b => b.Asset == asset);
            if (createIfNotExist && balance == null)
            {
                balance = new Balance { Asset = asset };
                account.Balances.Add(balance);
            }
            return balance;
        }
    }
}
