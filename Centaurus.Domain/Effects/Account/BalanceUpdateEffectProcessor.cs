using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceUpdateEffectProcesor : BaseAccountEffectProcessor<BalanceUpdateEffect>
    {
        public BalanceUpdateEffectProcesor(BalanceUpdateEffect effect, AccountWrapper account)
            : base(effect, account)
        {

        }
        public override void CommitEffect()
        {
            MarkAsProcessed();
            var balance = AccountWrapper.Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            var balance = AccountWrapper.Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(-Effect.Amount);
        }
    }
}
