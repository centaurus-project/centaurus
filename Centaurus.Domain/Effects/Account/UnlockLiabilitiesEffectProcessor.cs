using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class UnlockLiabilitiesEffectProcessor : BaseAccountEffectProcessor<UnlockLiabilitiesEffect>
    {
        public UnlockLiabilitiesEffectProcessor(UnlockLiabilitiesEffect effect, AccountStorage accountStorage)
            : base(effect, accountStorage)
        {

        }

        public override void CommitEffect()
        {
            var balance = Account.GetBalance(Effect.Asset);
            balance.UnlockLiabilities(Effect.Amount);
        }

        public override void RevertEffect()
        {
            Account.GetBalance(Effect.Asset).LockLiabilities(Effect.Amount);
        }
    }
}
