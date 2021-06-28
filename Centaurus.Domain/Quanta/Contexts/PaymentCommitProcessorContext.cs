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
        public PaymentCommitProcessorContext(EffectProcessorsContainer effectProcessors) 
            : base(effectProcessors)
        {
            var payment = (PaymentCommitQuantum)effectProcessors.Envelope.Message;
            if (!CentaurusContext.PaymentProvidersManager.TryGetManager(payment.Source.ProviderId, out var paymentProvider))
                throw new Exception($"Unable to find payment provider {payment.Source.ProviderId}");
            PaymentProvider = paymentProvider;
        }

        public Dictionary<Withdrawal, WithdrawalWrapper> Withdrawals { get; } = new Dictionary<Withdrawal, WithdrawalWrapper>();

        public PaymentProviderBase PaymentProvider { get; }
    }
}
