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
            account.GetBalance(Effect.Asset).LockLiabilities(Effect.Amount);
        }

        public override void RevertEffect()
        {
            account.GetBalance(Effect.Asset).UnlockLiabilities(Effect.Amount);
        }

        public static LockLiabilitiesEffectProcessor GetProcessor(ulong apex, Account account, int asset, long amount)
        {
            return new LockLiabilitiesEffectProcessor(
                new LockLiabilitiesEffect { Amount = amount, Asset = asset, Pubkey = account.Pubkey, Apex = apex },
                account
            );
        }
    }
}
