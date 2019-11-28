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
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException();
        }

        public override void CommitEffect()
        {
            withdrawalStorage.Add(Effect.Withdrawal);
        }

        public override void RevertEffect()
        {
            withdrawalStorage.Remove(Effect.Withdrawal.TransactionHash);
        }
    }
}
