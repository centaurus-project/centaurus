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

            if (!CentaurusContext.PaymentsManager.TryGetManager(WithdrawalRequest.PaymentProvider, out var paymentsManager))
                throw new BadRequestException($"Provider {WithdrawalRequest.PaymentProvider} is not supported.");

            PaymentsManager = paymentsManager;

            if (!PaymentsManager.PaymentsParser.TryDeserializeTransaction(WithdrawalRequest.TransactionXdr, out var transaction))
                throw new BadRequestException($"Invalid transaction data.");

            Transaction = transaction;
        }

        public WithdrawalWrapper Withdrawal { get; set; }

        public WithdrawalRequest WithdrawalRequest { get; }

        public TransactionWrapper Transaction { get; }

        public PaymentsProviderBase PaymentsManager { get; }
    }

    public class TransactionWrapper
    {
        public virtual object Transaction { get; set; }

        public byte[] Hash { get; set; }

        public long MaxTime { get; set; }
    }

    public class TransactionWrapper<T>: TransactionWrapper
    {
        public new T Transaction { get; set; }
    }
}