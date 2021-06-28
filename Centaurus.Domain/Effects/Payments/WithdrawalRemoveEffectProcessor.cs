using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalRemoveEffectProcessor : ClientEffectProcessor<WithdrawalRemoveEffect>
    {
        private WithdrawalStorage withdrawalStorage;
        private WithdrawalWrapper withdrawal;

        public WithdrawalRemoveEffectProcessor(WithdrawalRemoveEffect effect, AccountWrapper accountWrapper, WithdrawalWrapper withdrawal, WithdrawalStorage withdrawalStorage)
            : base(effect, accountWrapper)
        {
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
            this.withdrawal = withdrawal ?? throw new ArgumentNullException(nameof(withdrawal));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            AccountWrapper.Account.Withdrawal = 0;
            AccountWrapper.Withdrawal = null;
            withdrawalStorage.Remove(withdrawal.Hash);

            foreach (var withdrawalItem in Effect.Items)
            {
                var balance = AccountWrapper.Account.GetBalance(withdrawalItem.Asset);
                if (Effect.IsSuccessful)
                    balance.UpdateBalance(-withdrawalItem.Amount);
                balance.UpdateLiabilities(-withdrawalItem.Amount);
            }
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Add(withdrawal);
            withdrawal.AccountWrapper.Withdrawal = withdrawal;
            withdrawal.AccountWrapper.Account.Withdrawal = withdrawal.Apex;

            foreach (var withdrawalItem in Effect.Items)
            {
                var balance = AccountWrapper.Account.GetBalance(withdrawalItem.Asset);
                if (Effect.IsSuccessful)
                    balance.UpdateBalance(withdrawalItem.Amount);
                balance.UpdateLiabilities(withdrawalItem.Amount);
            }
        }
    }
}
