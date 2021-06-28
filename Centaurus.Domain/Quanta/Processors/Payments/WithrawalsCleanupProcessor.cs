using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class WithrawalsCleanupProcessor : QuantumProcessor<WithdrawalCleanupProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.WithrawalsCleanup;

        public override WithdrawalCleanupProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new WithdrawalCleanupProcessorContext(container);
        }

        public override Task<QuantumResultMessage> Process(WithdrawalCleanupProcessorContext context)
        {
            context.EffectProcessors.AddWithdrawalRemove(context.Withdrawal, false, context.PaymentProvider.WithdrawalStorage);
            return Task.FromResult((QuantumResultMessage)context.Envelope.CreateResult(ResultStatusCodes.Success));
        }

        public override Task Validate(WithdrawalCleanupProcessorContext context)
        {
            var cleanup = (WithrawalsCleanupQuantum)context.Envelope.Message;
            if (cleanup.ExpiredWithdrawal == null)
                throw new InvalidOperationException("No withdrawal was specified.");

            var withdrawal = context.PaymentProvider.WithdrawalStorage.GetWithdrawal(cleanup.ExpiredWithdrawal);
            if (withdrawal == null)
                throw new InvalidOperationException("Withdrawal is missing.");
            context.Withdrawal = withdrawal;

            return Task.CompletedTask;
        }
    }
}
