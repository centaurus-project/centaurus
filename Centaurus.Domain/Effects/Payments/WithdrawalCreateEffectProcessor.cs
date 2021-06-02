using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalCreateEffectProcessor : EffectProcessor<WithdrawalCreateEffect>
    {
        private WithdrawalStorage withdrawalsStorage;
        private WithdrawalWrapper withdrawal;

        public WithdrawalCreateEffectProcessor(WithdrawalCreateEffect effect, WithdrawalWrapper withdrawal, WithdrawalStorage withdrawalsStorage)
            :base(effect)
        {
            this.withdrawalsStorage = withdrawalsStorage ?? throw new ArgumentNullException(nameof(withdrawalsStorage));
            this.withdrawal = withdrawal ?? throw new ArgumentNullException(nameof(withdrawal));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawalsStorage.Add(withdrawal);
            withdrawal.Source.Withdrawal = withdrawal;
            withdrawal.Source.Account.Withdrawal = withdrawal.Apex;
            foreach (var withdrawalItem in Effect.Items)
                Effect.AccountWrapper.Account.GetBalance(withdrawalItem.Asset).UpdateLiabilities(withdrawalItem.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawal.Source.Account.Withdrawal = 0;
            withdrawal.Source.Withdrawal = null;
            withdrawalsStorage.Remove(withdrawal.Hash);
            foreach (var withdrawalItem in Effect.Items)
                Effect.AccountWrapper.Account.GetBalance(withdrawalItem.Asset).UpdateLiabilities(-withdrawalItem.Amount);
        }
    }
}
