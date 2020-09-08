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
            foreach (var withdrawal in context.Withdrawals)
            {
                context.EffectProcessors.AddWithdrawalRemove(withdrawal, Global.WithdrawalStorage);
                foreach (var withdrawalItem in withdrawal.Withdrawals)
                {
                    context.EffectProcessors.AddUnlockLiabilities(withdrawal.Source.Account, withdrawalItem.Asset, withdrawalItem.Amount);
                }
            }
            return Task.FromResult(context.Envelope.CreateResult(ResultStatusCodes.Success, context.EffectProcessors.GetEffects().ToList()));
        }

        public override Task Validate(WithdrawalCleanupProcessorContext context)
        {
            var cleanup = (WithrawalsCleanupQuantum)context.Envelope.Message;
            if (cleanup.ExpiredWithdrawals.Count < 1)
                throw new InvalidOperationException("No withdrawals were specified.");

            if (cleanup.ExpiredWithdrawals.GroupBy(w => w, new ByteArrayComparer()).Any(g => g.Count() > 1))
                throw new InvalidOperationException("All withdrawals should be unique.");

            foreach (var withdrawalHash in cleanup.ExpiredWithdrawals)
            {
                var withdrawal = Global.WithdrawalStorage.GetWithdrawal(withdrawalHash);
                if (withdrawal == null)
                    throw new InvalidOperationException("Withdrawal is missing.");
                context.Withdrawals.Add(withdrawal);
            }
            return Task.CompletedTask;
        }
    }
}
