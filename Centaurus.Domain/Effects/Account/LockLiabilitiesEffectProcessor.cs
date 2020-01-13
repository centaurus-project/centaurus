using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class LockLiabilitiesEffectProcessor : BaseAccountEffectProcessor<LockLiabilitiesEffect>
    {
        public LockLiabilitiesEffectProcessor(LockLiabilitiesEffect effect, AccountStorage accountStorage)
            : base(effect, accountStorage)
        {

        }

        public override void CommitEffect()
        {
            var balance = Account.GetBalance(Effect.Asset);
            balance.LockLiabilities(Effect.Amount);
        }

        public override void RevertEffect()
        {
            Account.GetBalance(Effect.Asset).UnlockLiabilities(Effect.Amount);
        }
    }
}
