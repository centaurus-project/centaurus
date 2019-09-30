using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class AccountStorage
    {
        public AccountStorage(IEnumerable<Account> accounts)
        {
            if (accounts == null)
                accounts = new Account[] { };

            this.accounts = new ConcurrentDictionary<RawPubKey, Account>(accounts.ToDictionary(m => m.Pubkey));
        }

        ConcurrentDictionary<RawPubKey, Account> accounts = new ConcurrentDictionary<RawPubKey, Account>();

        public void Add(Account account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            if (!accounts.TryAdd(account.Pubkey, account))
                throw new Exception("Account with specified public key already exists");
        }

        /// <summary>
        /// Retrieve account record by its public key.
        /// </summary>
        /// <param name="pubkey">Account public key</param>
        /// <returns>Account record, or null if not found</returns>
        public Account GetAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));
            return accounts.GetValueOrDefault(pubkey);
        }


        private readonly object syncRoot = new { };
        public Account CreateAccount(RawPubKey pubkey, List<Balance> balances)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            lock (syncRoot)
            {
                var acc = GetAccount(pubkey);
                if (acc != null)
                    throw new InvalidOperationException("Account already exists");

                acc = new Account
                {
                    Pubkey = pubkey,
                    Balances = balances?.ToList() ?? new List<Balance>()
                };
                Add(acc);
                return acc;
            }
        }

        public IEnumerable<Account> GetAll()
        {
            return accounts.Values;
        }
    }
}
