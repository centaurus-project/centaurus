using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PaymentCommitProcessorContext : ProcessorContext
    {
        public PaymentCommitProcessorContext(ExecutionContext context, MessageEnvelope quantum, AccountWrapper account) 
            : base(context, quantum, account)
        {
            var payment = (DepositQuantum)Quantum;
            if (!CentaurusContext.PaymentProvidersManager.TryGetManager(payment.Source.ProviderId, out var paymentProvider))
                throw new Exception($"Unable to find payment provider {payment.Source.ProviderId}");
            PaymentProvider = paymentProvider;
        }

        public PaymentProviderBase PaymentProvider { get; }
    }
}
