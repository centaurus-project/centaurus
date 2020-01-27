using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class AccountExtensions
    {
        /// <summary>
        /// Checks if the account has balance for the specified asset.
        /// </summary>
        /// <param name="account">Account record</param>
        /// <param name="asset">Asset id</param>
        /// <returns></returns>
        public static bool HasBalance(this Account account, int asset)
        {
            return account.Balances.Any(b => b.Asset == asset);
        }

        /// <summary>
        /// Creates and adds created balance to the acount
        /// </summary>
        /// <param name="account">Account record</param>
        /// <param name="asset">Asset id</param>
        /// <returns>Created balance</returns>
        public static Balance CreateBalance(this Account account, int asset)
        {
            var balance = new Balance { Asset = asset, Account = account };
            account.Balances.Add(balance);
            return balance;
        }

        /// <summary>
        /// Retrieve account balance.
        /// </summary>
        /// <param name="account">Account record</param>
        /// <param name="asset">Asset id</param>
        /// <returns></returns>
        public static Balance GetBalance(this Account account, int asset)
        {
            return account.Balances.Find(b => b.Asset == asset);
        }
    }
}
