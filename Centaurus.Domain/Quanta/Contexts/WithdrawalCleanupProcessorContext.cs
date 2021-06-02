using Centaurus.Models;
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

            if (!effectProcessorsContainer.Context.PaymentsManager.TryGetManager(cleanup.Provider, out var paymentsProvider))
                throw new InvalidOperationException($"Unable to find manager for provider {cleanup.Provider}.");

            PaymentsProvider = paymentsProvider;
        }

        public WithdrawalWrapper Withdrawal { get; set; }

        public PaymentsProviderBase PaymentsProvider { get; }
    }
}
