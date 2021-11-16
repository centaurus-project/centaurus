using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public class AccountStorage
    {
        public AccountStorage(IEnumerable<Account> accounts)
        {
            if (accounts == null)
                accounts = new Account[] { };

            this.accounts = new Dictionary<RawPubKey, Account>(accounts.ToDictionary(m => m.Pubkey));
        }
        
        readonly Dictionary<RawPubKey, Account> accounts = new Dictionary<RawPubKey, Account>();

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


        public Account CreateAccount(RawPubKey pubkey, RequestRateLimits rateLimits)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (accounts.ContainsKey(pubkey))
                throw new InvalidOperationException($"Account with public key {pubkey} already exists");

            var acc = new Account(rateLimits) {
                Pubkey = pubkey,
                Balances = new Dictionary<string, Balance>(),
                Orders = new Dictionary<ulong, Order>()
            };
            accounts.Add(pubkey, acc);

            return acc;
        }

        public int Count => accounts.Count;

        public void RemoveAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (!accounts.Remove(pubkey))
                throw new InvalidOperationException($"Account with id {pubkey} doesn't exist");
        }

        public IEnumerable<Account> GetAll()
        {
            return accounts.Values;
        }
    }
}
