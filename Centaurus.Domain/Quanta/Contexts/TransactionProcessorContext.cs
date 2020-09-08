using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TransactionProcessorContext: ProcessorContext
    {

        private Transaction transaction;

        public TransactionProcessorContext(EffectProcessorsContainer effectProcessors) : base(effectProcessors)
        {
        }

        public Transaction Transaction
        {
            get => transaction;
            set
            {
                if (value != null && value != transaction)
                {
                    transaction = value;
                    TransactionHash = transaction.Hash();
                }
            }
        }

        public byte[] TransactionHash { get; private set; }
    }
}
