﻿using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalRemoveEffectProcessor : EffectProcessor<WithdrawalRemoveEffect>
    {
        private WithdrawalStorage withdrawalStorage;
        private WithdrawalWrapper withdrawal;

        public WithdrawalRemoveEffectProcessor(WithdrawalRemoveEffect effect, WithdrawalWrapper withdrawal, WithdrawalStorage withdrawalStorage)
            : base(effect)
        {
            this.withdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
            this.withdrawal = withdrawal ?? throw new ArgumentNullException(nameof(withdrawal));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            withdrawal.Source.Account.Withdrawal = 0;
            withdrawal.Source.Withdrawal = null;
            withdrawalStorage.Remove(withdrawal.Hash);

            foreach (var withdrawalItem in Effect.Items)
            {
                var balance = Effect.AccountWrapper.Account.GetBalance(withdrawalItem.Asset);
                if (Effect.IsSuccessful)
                    balance.UpdateBalance(-withdrawalItem.Amount);
                balance.UpdateLiabilities(-withdrawalItem.Amount);
            }
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            withdrawalStorage.Add(withdrawal);
            withdrawal.Source.Withdrawal = withdrawal;
            withdrawal.Source.Account.Withdrawal = withdrawal.Apex;

            foreach (var withdrawalItem in Effect.Items)
            {
                var balance = Effect.AccountWrapper.Account.GetBalance(withdrawalItem.Asset);
                if (Effect.IsSuccessful)
                    balance.UpdateBalance(withdrawalItem.Amount);
                balance.UpdateLiabilities(withdrawalItem.Amount);
            }
        }
    }
}
