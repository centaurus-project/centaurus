using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public class AccountStorage
    {
        public AccountStorage(IEnumerable<AccountWrapper> accounts)
        {
            if (accounts == null)
                accounts = new AccountWrapper[] { };

            this.accounts = new Dictionary<ulong, AccountWrapper>(accounts.ToDictionary(m => m.Id));
            this.accountIds = new Dictionary<RawPubKey, ulong>(accounts.ToDictionary(m => m.Pubkey, v => v.Id));
        }
        
        readonly Dictionary<RawPubKey, ulong> accountIds = new Dictionary<RawPubKey, ulong>();
        readonly Dictionary<ulong, AccountWrapper> accounts = new Dictionary<ulong, AccountWrapper>();

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
        public AccountWrapper GetAccount(ulong id)
        {
            if (id == default)
                throw new ArgumentNullException(nameof(id));
            return accounts.GetValueOrDefault(id);
        }

        public AccountWrapper CreateAccount(ulong id, RawPubKey pubkey, RequestRateLimits rateLimits)
        {
            if (pubkey == null)
                throw new ArgumentNullException(nameof(pubkey));

            if (accountIds.ContainsKey(pubkey))
                throw new InvalidOperationException($"Account with public key {pubkey} already exists");

            var acc = new AccountWrapper(rateLimits) {
                Id = id,
                Pubkey = pubkey,
                Balances = new Dictionary<string, Balance>(),
                Orders = new Dictionary<ulong, Order>()
            };
            accountIds.Add(pubkey, id);
            accounts.Add(id, acc);

            return acc;
        }

        public ulong NextAccountId => LastAccountId + 1;

        public ulong LastAccountId => accountIds.Values.LastOrDefault();

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
