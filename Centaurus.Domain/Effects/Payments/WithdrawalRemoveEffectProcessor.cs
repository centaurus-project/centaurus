using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalRemoveEffectProcessor : EffectProcessor<WithdrawalRemoveEffect>
    {
        private WithdrawalStorage withdrawalStorage;

        public WithdrawalRemoveEffectProcessor(WithdrawalRemoveEffect effect, WithdrawalStorage withdrawalStorage)
            : base(effect)
        {
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Remove(Effect.Withdrawal.TransactionHash);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Add(Effect.Withdrawal);
        }
    }
}
