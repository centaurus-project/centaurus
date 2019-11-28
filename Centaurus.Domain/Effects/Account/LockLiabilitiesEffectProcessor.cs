using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class LockLiabilitiesEffectProcessor : BaseAccountEffectProcessor<LockLiabilitiesEffect>
    {
        public LockLiabilitiesEffectProcessor(LockLiabilitiesEffect effect, Account account)
            : base(effect, account)
        {

        }

        public override void CommitEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.LockLiabilities(Effect.Amount);
        }

        public override void RevertEffect()
        {
            account.GetBalance(Effect.Asset).UnlockLiabilities(Effect.Amount);
        }
    }
}
