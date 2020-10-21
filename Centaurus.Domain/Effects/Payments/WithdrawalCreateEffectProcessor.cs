using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalCreateEffectProcessor : EffectProcessor<WithdrawalCreateEffect>
    {
        private WithdrawalStorage withdrawalStorage;
        private Withdrawal withdrawal;

        public WithdrawalCreateEffectProcessor(WithdrawalCreateEffect effect, Withdrawal withdrawal, WithdrawalStorage withdrawalStorage)
            :base(effect)
        {
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
            this.withdrawal = withdrawal ?? throw new ArgumentNullException(nameof(withdrawal));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Add(withdrawal);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Remove(withdrawal.Hash);
        }
    }
}
