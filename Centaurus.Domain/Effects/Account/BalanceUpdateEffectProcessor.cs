using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceUpdateEffectProcesor : BaseAccountEffectProcessor<BalanceUpdateEffect>
    {
        public BalanceUpdateEffectProcesor(BalanceUpdateEffect effect, AccountStorage accountStorage)
            : base(effect, accountStorage)
        {

        }
        public override void CommitEffect()
        {
            var balance = Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount);
        }

        public override void RevertEffect()
        {
            var balance = Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(-Effect.Amount);
        }
    }
}
