using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public abstract class PaymentParserBase
    {
        public string Provider { get; }

        public abstract int CompareCursors(string left, string right);

        public abstract WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, AccountWrapper account, TransactionWrapper transactionWrapper, ProviderSettings constellationSettings);

        public abstract bool TryDeserializeTransaction(byte[] rawTransaction, out TransactionWrapper transaction);
    }
}
