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
        public WithdrawalStorage(IEnumerable<PaymentRequestBase> payments)
        {
            withdrawals = new ConcurrentDictionary<byte[], PaymentRequestBase>(new HashComparer());

            if (payments == null)
                return;
            foreach (var payment in payments)
            {
                withdrawals.TryAdd(payment.TransactionHash, payment);
            }
        }

        ConcurrentDictionary<byte[], PaymentRequestBase> withdrawals;

        public void Add(PaymentRequestBase payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));

            if (!withdrawals.TryAdd(payment.TransactionHash, payment))
                throw new Exception("Payment with specified transaction hash already exists");
        }

        public PaymentRequestBase GetWithdrawal(byte[] transactionHash)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));

            return withdrawals.GetValueOrDefault(transactionHash);
        }

        public void Clear()
        {
            withdrawals.Clear();
        }

        public IEnumerable<PaymentRequestBase> GetAll()
        {
            return withdrawals.Values;
        }
    }
}
