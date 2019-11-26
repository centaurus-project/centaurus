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
        public WithdrawalStorage(IEnumerable<RequestQuantum> payments)
        {
            withdrawals = new ConcurrentDictionary<byte[], RequestQuantum>(new HashComparer());

            if (payments == null)
                return;
            foreach (var payment in payments)
            {
                Add(payment);
            }
        }

        ConcurrentDictionary<byte[], RequestQuantum> withdrawals;

        public void Add(RequestQuantum payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (!(payment.RequestEnvelope.Message is PaymentRequestBase)) throw new ArgumentException("Message is not PaymentRequestBase");

            var currentPaymentHash = ((PaymentRequestBase)payment.RequestEnvelope.Message).TransactionHash;
            if (!withdrawals.TryAdd(currentPaymentHash, payment))
                throw new Exception("Payment with specified transaction hash already exists");
        }

        public RequestQuantum GetWithdrawal(byte[] transactionHash)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));

            return withdrawals.GetValueOrDefault(transactionHash);
        }

        public void Clear()
        {
            withdrawals.Clear();
        }

        public IEnumerable<RequestQuantum> GetAll()
        {
            return withdrawals.Values;
        }
    }
}
