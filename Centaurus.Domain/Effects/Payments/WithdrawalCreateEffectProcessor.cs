using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalCreateEffectProcessor : EffectProcessor<WithdrawalCreateEffect>
    {
        private WithdrawalStorage withdrawalStorage;

        public WithdrawalCreateEffectProcessor(WithdrawalCreateEffect effect, WithdrawalStorage withdrawalStorage)
            :base(effect)
        {
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Add(Effect.Withdrawal);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Remove(Effect.Withdrawal.TransactionHash);
        }
    }
}
