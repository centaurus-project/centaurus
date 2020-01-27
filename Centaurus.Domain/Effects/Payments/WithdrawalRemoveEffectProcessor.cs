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
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException();
        }

        public override void CommitEffect()
        {
            withdrawalStorage.Remove(Effect.Withdrawal.TransactionHash);
        }

        public override void RevertEffect()
        {
            withdrawalStorage.Add(Effect.Withdrawal);
        }
    }
}
