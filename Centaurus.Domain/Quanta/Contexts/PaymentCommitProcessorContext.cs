using Centaurus.Models;
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
            if (!CentaurusContext.PaymentsManager.TryGetManager(payment.Source.Provider, out var paymentsProvider))
                throw new Exception($"Unable to find payment provider {payment.Source.Provider}");
            PaymentProvider = paymentsProvider;
        }

        public Dictionary<Withdrawal, WithdrawalWrapper> Withdrawals { get; } = new Dictionary<Withdrawal, WithdrawalWrapper>();

        public PaymentsProviderBase PaymentProvider { get; }
    }
}
