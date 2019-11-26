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

            this.accounts = new Dictionary<RawPubKey, Account>(accounts.ToDictionary(m => m.Pubkey));
        }

        Dictionary<RawPubKey, Account> accounts = new Dictionary<RawPubKey, Account>();

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

        public Account CreateAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (accounts.ContainsKey(pubkey))
                throw new InvalidOperationException($"Account with public key {pubkey} already exists");

            var acc = new Account
            {
                Pubkey = pubkey,
                Balances = new List<Balance>()
            };
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

        public IEnumerable<Account> GetAll()
        {
            return accounts.Values;
        }
    }
}
