﻿using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;

namespace Centaurus.Domain
{
    public class WithdrawalProcessorContext : RequestContext, ITransactionProcessorContext
    {
        public WithdrawalProcessorContext(ExecutionContext context, Quantum quantum, AccountWrapper account)
            : base(context, quantum, account)
        {
            if (!CentaurusContext.PaymentProvidersManager.TryGetManager(WithdrawalRequest.Provider, out var provider))
                throw new BadRequestException($"Provider {WithdrawalRequest.Provider} is not supported.");

            PaymentProvider = provider;
        }

        public RequestTransactionQuantum TransactionQuantum => (RequestTransactionQuantum)Request;

        public WithdrawalRequest WithdrawalRequest => (WithdrawalRequest)Request.RequestEnvelope.Message;

        public PaymentProviderBase PaymentProvider { get; }

        public byte[] Transaction => TransactionQuantum.Transaction;
    }
}