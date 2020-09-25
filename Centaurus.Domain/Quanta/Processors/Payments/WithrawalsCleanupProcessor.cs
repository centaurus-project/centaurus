using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class WithrawalsCleanupProcessor : QuantumRequestProcessor<WithdrawalCleanupProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.WithrawalsCleanup;

        public override WithdrawalCleanupProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new WithdrawalCleanupProcessorContext(container);
        }

        public override Task<ResultMessage> Process(WithdrawalCleanupProcessorContext context)
        {
            context.EffectProcessors.AddWithdrawalRemove(context.Withdrawal, Global.WithdrawalStorage);
            foreach (var withdrawalItem in context.Withdrawal.Withdrawals)
            {
                context.EffectProcessors.AddUnlockLiabilities(context.Withdrawal.Source.Account, withdrawalItem.Asset, withdrawalItem.Amount);
            }
            return Task.FromResult(context.Envelope.CreateResult(ResultStatusCodes.Success, context.EffectProcessors.GetEffects().ToList()));
        }

        public override Task Validate(WithdrawalCleanupProcessorContext context)
        {
            var cleanup = (WithrawalsCleanupQuantum)context.Envelope.Message;
            if (cleanup.ExpiredWithdrawal == null)
                throw new InvalidOperationException("No withdrawal was specified.");

            var withdrawal = Global.WithdrawalStorage.GetWithdrawal(cleanup.ExpiredWithdrawal);
            if (withdrawal == null)
                throw new InvalidOperationException("Withdrawal is missing.");
            context.Withdrawal = withdrawal;

            return Task.CompletedTask;
        }
    }
}
