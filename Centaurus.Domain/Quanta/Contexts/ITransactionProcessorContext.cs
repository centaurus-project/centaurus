using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface ITransactionProcessorContext
    {
        public Transaction Transaction { get; set; }

        public byte[] TransactionHash { get; set; }
    }
}
