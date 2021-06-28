using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;

namespace Centaurus.Domain
{
    public class WithdrawalCleanupProcessorContext : ProcessorContext
    {
        public WithdrawalCleanupProcessorContext(EffectProcessorsContainer effectProcessorsContainer)
            : base(effectProcessorsContainer)
        {
            var cleanup = (WithrawalsCleanupQuantum)effectProcessorsContainer.Envelope.Message;
            if (cleanup.ExpiredWithdrawal == null)
                throw new InvalidOperationException("No withdrawal was specified.");

            if (!effectProcessorsContainer.Context.PaymentProvidersManager.TryGetManager(cleanup.ProviderId, out var paymentProvider))
                throw new InvalidOperationException($"Unable to find manager for provider {cleanup.ProviderId}.");

            PaymentProvider = paymentProvider;
        }

        public WithdrawalWrapper Withdrawal { get; set; }

        public PaymentProviderBase PaymentProvider { get; }
    }
}
