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

        public override UpdatedObject[] CommitEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.UnlockLiabilities(Effect.Amount);
            return new UpdatedObject[] { new UpdatedObject(balance) };
        }

        public override void RevertEffect()
        {
            account.GetBalance(Effect.Asset).LockLiabilities(Effect.Amount);
        }

        public static UnlockLiabilitiesEffectProcessor GetProcessor(ulong apex, Account account, int asset, long amount)
        {
            return new UnlockLiabilitiesEffectProcessor(
                new UnlockLiabilitiesEffect { Amount = amount, Asset = asset, Pubkey = account.Pubkey, Apex = apex },
                account
            );
        }

        public static UnlockLiabilitiesEffectProcessor GetProcessor(UnlockLiabilitiesEffect effect, Account account)
        {
            return new UnlockLiabilitiesEffectProcessor(effect, account);
        }
    }
}
