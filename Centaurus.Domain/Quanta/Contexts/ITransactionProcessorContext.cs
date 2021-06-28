using Centaurus.PaymentProvider;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface ITransactionProcessorContext
    {
        public TransactionWrapper Transaction { get; }

        public PaymentProviderBase PaymentProvider { get; }
    }
}
