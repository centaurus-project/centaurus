using Centaurus.Domain.Models;
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
        public AccountStorage(IEnumerable<AccountWrapper> accounts)
        {
            if (accounts == null)
                accounts = new AccountWrapper[] { };

            this.accounts = new Dictionary<int, AccountWrapper>(accounts.ToDictionary(m => m.Account.Id));
            this.accountIds = new Dictionary<RawPubKey, int>(accounts.ToDictionary(m => m.Account.Pubkey, v => v.Account.Id));
        }
        
        readonly Dictionary<RawPubKey, int> accountIds = new Dictionary<RawPubKey, int>();
        readonly Dictionary<int, AccountWrapper> accounts = new Dictionary<int, AccountWrapper>();

        /// <summary>
        /// Retrieve account record by its public key.
        /// </summary>
        /// <param name="pubkey">Account public key</param>
        /// <returns>Account record, or null if not found</returns>
        public AccountWrapper GetAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));
            var accId = accountIds.GetValueOrDefault(pubkey);
            if (accId == default)
                return null;
            return GetAccount(accId);
        }

        /// <summary>
        /// Retrieve account record by its public id.
        /// </summary>
        /// <param name="id">Account id</param>
        /// <returns>Account record, or null if not found</returns>
        public AccountWrapper GetAccount(int id)
        {
            if (id == default)
                throw new ArgumentNullException(nameof(id));
            return accounts.GetValueOrDefault(id);
        }

        public AccountWrapper CreateAccount(int id, RawPubKey pubkey, RequestRateLimits rateLimits)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (accountIds.ContainsKey(pubkey))
                throw new InvalidOperationException($"Account with public key {pubkey} already exists");

            var acc = new AccountWrapper(new Account
            {
                Id = id,
                Pubkey = pubkey,
                Balances = new List<Balance>()
            },
                rateLimits
            );
            accountIds.Add(pubkey, id);
            accounts.Add(id, acc);

            return acc;
        }

        public int NextAccountId => LastAccountId + 1;

        public int LastAccountId => accountIds.Values.LastOrDefault();

        public int Count => accountIds.Count;

        public void RemoveAccount(RawPubKey pubkey)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (!accountIds.TryGetValue(pubkey, out var id))
                throw new InvalidOperationException($"Account with public key {pubkey} doesn't exist");

            if (!accounts.Remove(id))
                throw new InvalidOperationException($"Account with id {id} doesn't exist");

            if (!accountIds.Remove(pubkey))
                throw new Exception($"Account with public key {pubkey} doesn't exist");
        }

        public IEnumerable<AccountWrapper> GetAll()
        {
            return accounts.Values;
        }
    }
}
