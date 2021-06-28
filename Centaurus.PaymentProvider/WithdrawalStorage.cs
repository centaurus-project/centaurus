using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public class WithdrawalStorage
    {
        private object withdrawalSyncRoot = new { };
        private Dictionary<byte[], WithdrawalWrapper> withdrawals = new Dictionary<byte[], WithdrawalWrapper>(new HashComparer());

        public virtual void Add(WithdrawalWrapper withdrawal)
        {
            if (withdrawal == null)
                throw new ArgumentNullException(nameof(withdrawal));

            lock (withdrawalSyncRoot)
            {
                if (!withdrawals.TryAdd(withdrawal.Hash, withdrawal))
                    throw new Exception("Payment with specified transaction hash already exists");
            }
        }

        public void Remove(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            lock (withdrawalSyncRoot)
            {
                if (!withdrawals.Remove(transactionHash, out var withdrawal))
                    throw new Exception("Withdrawal with specified hash is not found");
            }
        }

        public List<WithdrawalWrapper> GetAll()
        {
            lock (withdrawalSyncRoot)
            {
                return withdrawals.Values.Select(w => w).ToList();
            }
        }

        public WithdrawalWrapper GetWithdrawal(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            lock (withdrawalSyncRoot)
            {
                return withdrawals.GetValueOrDefault(transactionHash);
            }
        }

        public WithdrawalWrapper GetWithdrawal(long apex)
        {
            if (apex == default)
                throw new ArgumentNullException(nameof(apex));

            lock (withdrawalSyncRoot)
            {
                return withdrawals.Values.FirstOrDefault(w => w.Apex == apex);
            }
        }
    }
}
