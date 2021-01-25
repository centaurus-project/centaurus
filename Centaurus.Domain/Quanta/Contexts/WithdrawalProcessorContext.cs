using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalProcessorContext : RequestContext, ITransactionProcessorContext
    {
        public WithdrawalProcessorContext(EffectProcessorsContainer effectProcessorsContainer)
            : base(effectProcessorsContainer)
        {
            WithdrawalRequest = (WithdrawalRequest)Request.RequestEnvelope.Message;
        }

        public List<WithdrawalWrapperItem> WithdrawalItems { get; set; } = new List<WithdrawalWrapperItem>();

        public WithdrawalRequest WithdrawalRequest { get; }

        private Transaction transaction;
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

        public byte[] TransactionHash { get; set; }
    }
}