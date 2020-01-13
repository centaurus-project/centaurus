using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalStorage
    {
        public WithdrawalStorage(IEnumerable<Withdrawal> payments)
        {
            withdrawals = new Dictionary<byte[], Withdrawal>(new HashComparer());

            if (payments == null)
                return;
            foreach (var payment in payments)
            {
                Add(payment);
            }
        }

        Dictionary<byte[], Withdrawal> withdrawals;

        public void Add(Withdrawal payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));

            if (!withdrawals.TryAdd(payment.TransactionHash, payment))
                throw new Exception("Payment with specified transaction hash already exists");
        }

        public void Remove(byte[] transactionHash)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));

            if (!withdrawals.Remove(transactionHash))
                throw new Exception("Withdrawal with specified hash is not found");
        }

        public Withdrawal GetWithdrawal(byte[] transactionHash)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));

            return withdrawals.GetValueOrDefault(transactionHash);
        }

        public void Clear()
        {
            withdrawals.Clear();
        }

        public IEnumerable<Withdrawal> GetAll()
        {
            return withdrawals.Values;
        }
    }
}
