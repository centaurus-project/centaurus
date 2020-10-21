using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalRemoveEffectProcessor : EffectProcessor<WithdrawalRemoveEffect>
    {
        private WithdrawalStorage withdrawalStorage;
        private Withdrawal withdrawal;

        public WithdrawalRemoveEffectProcessor(WithdrawalRemoveEffect effect, Withdrawal withdrawal, WithdrawalStorage withdrawalStorage)
            : base(effect)
        {
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
            this.withdrawal = withdrawal ?? throw new ArgumentNullException(nameof(withdrawal));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Remove(withdrawal.Hash);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Add(withdrawal);
        }
    }
}
