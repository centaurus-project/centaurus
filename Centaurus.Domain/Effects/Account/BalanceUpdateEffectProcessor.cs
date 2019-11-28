using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceUpdateEffectProcesor : BaseAccountEffectProcessor<BalanceUpdateEffect>
    {
        public BalanceUpdateEffectProcesor(BalanceUpdateEffect effect, Account account)
            : base(effect, account)
        {

        }
        public override void CommitEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount);
        }

        public override void RevertEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.UpdateBalance(-Effect.Amount);
        }
    }
}
