using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    //public static class AccountManager
    //{
    //    public static void Init(IEnumerable<Account> accounts)
    //    {
    //        AccountStorage.Init(accounts);
    //    }

    //    /// <summary>
    //    /// Retrieve account record by its public key.
    //    /// </summary>
    //    /// <param name="pubkey">Account public key</param>
    //    /// <returns>Account record, or null if not found</returns>
    //    public static Account GetAccount(RawPubKey pubkey)
    //    {
    //        if (pubkey == null)
    //            throw new ArgumentNullException(nameof(pubkey));
    //        return AccountStorage.GetAccount(pubkey);
    //    }


    //    private static readonly object accountInsertSyncRoot = new { };
    //    public static Account CreateAccount(RawPubKey pubkey, List<Balance> balances)
    //    {
    //        if (pubkey == null)
    //            throw new ArgumentNullException(nameof(pubkey));

    //        lock (accountInsertSyncRoot)
    //        {
    //            var acc = GetAccount(pubkey);
    //            if (acc != null)
    //                throw new InvalidOperationException("Account already exists");

    //            acc = new Account
    //            {
    //                Pubkey = pubkey,
    //                Balances = balances?.ToList() ?? new List<Balance>()
    //            };
    //            AccountStorage.Add(acc);
    //            return acc;
    //        }
    //    }

    //    public static IEnumerable<Account> GetAllAccounts()
    //    {
    //        return AccountStorage.GetAll();
    //    }
    //}
}
