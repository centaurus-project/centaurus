using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalCreateEffectProcessor : EffectProcessor<WithdrawalCreateEffect>
    {
        private WithdrawalStorage withdrawalStorage;
        private WithdrawalWrapper withdrawal;

        public WithdrawalCreateEffectProcessor(WithdrawalCreateEffect effect, WithdrawalWrapper withdrawal, WithdrawalStorage withdrawalStorage)
            :base(effect)
        {
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
            this.withdrawal = withdrawal ?? throw new ArgumentNullException(nameof(withdrawal));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Add(withdrawal);
            withdrawal.Source.Withdrawal = withdrawal;
            withdrawal.Source.Account.Withdrawal = withdrawal.Apex;
            foreach (var withdrawalItem in withdrawal.Withdrawals)
                Effect.AccountWrapper.Account.GetBalance(withdrawalItem.Asset).UpdateLiabilities(withdrawalItem.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawal.Source.Account.Withdrawal = 0;
            withdrawal.Source.Withdrawal = null;
            withdrawalStorage.Remove(withdrawal.Hash);
            foreach (var withdrawalItem in withdrawal.Withdrawals)
                Effect.AccountWrapper.Account.GetBalance(withdrawalItem.Asset).UpdateLiabilities(-withdrawalItem.Amount);
        }
    }
}
