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

        public override UpdatedObject[] CommitEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.LockLiabilities(Effect.Amount);
            return new UpdatedObject[] { new UpdatedObject(balance) };
        }

        public override void RevertEffect()
        {
            account.GetBalance(Effect.Asset).UnlockLiabilities(Effect.Amount);
        }

        public static LockLiabilitiesEffectProcessor GetProcessor(ulong apex, Account account, int asset, long amount)
        {
            return GetProcessor(
                new LockLiabilitiesEffect { Amount = amount, Asset = asset, Pubkey = account.Pubkey, Apex = apex },
                account
            );
        }

        public static LockLiabilitiesEffectProcessor GetProcessor(LockLiabilitiesEffect effect, Account account)
        {
            return new LockLiabilitiesEffectProcessor(effect, account);
        }
    }
}
