using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
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

            if (!CentaurusContext.PaymentProvidersManager.TryGetManager(WithdrawalRequest.PaymentProvider, out var paymentsManager))
                throw new BadRequestException($"Provider {WithdrawalRequest.PaymentProvider} is not supported.");

            PaymentProvider = paymentsManager;

            if (!PaymentProvider.Parser.TryDeserializeTransaction(WithdrawalRequest.Transaction, out var transaction))
                throw new BadRequestException($"Invalid transaction data.");

            Transaction = transaction;
        }

        public WithdrawalWrapper Withdrawal { get; set; }

        public WithdrawalRequest WithdrawalRequest { get; }

        public TransactionWrapper Transaction { get; }

        public PaymentProviderBase PaymentProvider { get; }
    }
}