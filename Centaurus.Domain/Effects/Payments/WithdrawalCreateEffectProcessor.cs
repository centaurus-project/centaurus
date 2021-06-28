using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalCreateEffectProcessor : ClientEffectProcessor<WithdrawalCreateEffect>
    {
        private WithdrawalStorage withdrawalsStorage;
        private WithdrawalWrapper withdrawal;

        public WithdrawalCreateEffectProcessor(WithdrawalCreateEffect effect, AccountWrapper account, WithdrawalWrapper withdrawal, WithdrawalStorage withdrawalsStorage)
            :base(effect, account)
        {
            this.withdrawalsStorage = withdrawalsStorage ?? throw new ArgumentNullException(nameof(withdrawalsStorage));
            this.withdrawal = withdrawal ?? throw new ArgumentNullException(nameof(withdrawal));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawalsStorage.Add(withdrawal);
            AccountWrapper.Withdrawal = withdrawal;
            AccountWrapper.Account.Withdrawal = withdrawal.Apex;
            foreach (var withdrawalItem in Effect.Items)
                AccountWrapper.Account.GetBalance(withdrawalItem.Asset).UpdateLiabilities(withdrawalItem.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            AccountWrapper.Account.Withdrawal = 0;
            AccountWrapper.Withdrawal = null;
            withdrawalsStorage.Remove(withdrawal.Hash);
            foreach (var withdrawalItem in Effect.Items)
                AccountWrapper.Account.GetBalance(withdrawalItem.Asset).UpdateLiabilities(-withdrawalItem.Amount);
        }
    }
}
