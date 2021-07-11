using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceUpdateEffectProcesor : BaseAccountEffectProcessor<BalanceUpdateEffect>
    {
        private UpdateSign sign;

        public BalanceUpdateEffectProcesor(BalanceUpdateEffect effect, AccountWrapper account, UpdateSign sign)
            : base(effect, account)
        {
            this.sign = sign;
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            var balance = AccountWrapper.Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount, sign);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            var balance = AccountWrapper.Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount, sign.Opposite());
        }
    }
}
