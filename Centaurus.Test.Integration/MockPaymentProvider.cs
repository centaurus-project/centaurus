using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public class MockPaymentProvider : PaymentProviderBase
    {
        public MockPaymentProvider(ProviderSettings settings, dynamic config, WithdrawalStorage withdrawalStorage)
            : base(settings, (object)config, withdrawalStorage)
        {

        }

        public override void Dispose()
        {
            return;
        }

        public override void ValidateTransaction(TransactionWrapper transaction)
        {
            return;
        }

        public void AddPayment()
        {

        }
    }

    public class MockTransaction
    {
        public List<MockOperation> Operations { get; set; }
    }

    public abstract class MockOperation
    {
        public RawPubKey Source { get; set; }

        public string Asset { get; set; }

        public long Amount { get; set; }
    }

    public class MockPayment : MockOperation
    {
    }


    public class MockWithdrawal : MockOperation
    {
    }
}
