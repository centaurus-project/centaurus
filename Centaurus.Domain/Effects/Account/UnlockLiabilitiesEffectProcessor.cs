using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class UnlockLiabilitiesEffectProcessor : BaseAccountEffectProcessor<UnlockLiabilitiesEffect>
    {
        public UnlockLiabilitiesEffectProcessor(UnlockLiabilitiesEffect effect, Account account)
            : base(effect, account)
        {

        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            var balance = Account.GetBalance(Effect.Asset);
            balance.UnlockLiabilities(Effect.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            Account.GetBalance(Effect.Asset).LockLiabilities(Effect.Amount);
        }
    }
}
