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
        public AccountStorage(IEnumerable<Account> accounts, RequestRateLimits defaultRequestRateLimits)
            : this(accounts.Select(a => new AccountWrapper(a, a.RequestRateLimits ?? defaultRequestRateLimits)))
        {

        }
        public AccountStorage(IEnumerable<AccountWrapper> accounts)
        {
            if (accounts == null)
                accounts = new AccountWrapper[] { };

            this.accounts = new Dictionary<RawPubKey, AccountWrapper>(accounts.ToDictionary(m => m.Account.Pubkey));
        }

        Dictionary<RawPubKey, AccountWrapper> accounts = new Dictionary<RawPubKey, AccountWrapper>();

        /// <summary>
        /// Retrieve account record by its public key.
        /// </summary>
        /// <param name="pubkey">Account public key</param>
        /// <returns>Account record, or null if not found</returns>
        public AccountWrapper GetAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));
            return accounts.GetValueOrDefault(pubkey);
        }

        public AccountWrapper CreateAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (accounts.ContainsKey(pubkey))
                throw new InvalidOperationException($"Account with public key {pubkey} already exists");

            var acc = new AccountWrapper(new Account
                {
                    Pubkey = pubkey,
                    Balances = new List<Balance>()
                },
                Global.Constellation.RequestRateLimits
            );
            accounts.Add(pubkey, acc);

            return acc;
        }

        public void RemoveAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (!accounts.ContainsKey(pubkey))
                throw new InvalidOperationException($"Account with public key {pubkey} doesn't exist");

            if (!accounts.Remove(pubkey))
                throw new Exception($"Unable to remove the account with public key {pubkey}");
        }

        public IEnumerable<AccountWrapper> GetAll()
        {
            return accounts.Values;
        }
    }
}
