using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class NonceUpdateEffectProcessor : BaseAccountEffectProcessor<NonceUpdateEffect>
    {
        public NonceUpdateEffectProcessor(NonceUpdateEffect effect, Account account)
            : base(effect, account)
        {
        }

        public override UpdatedObject[] CommitEffect()
        {
            account.Nonce = Effect.Nonce;
            return new UpdatedObject[] { new UpdatedObject(account) };
        }

        public override void RevertEffect()
        {
            account.Nonce = Effect.PrevNonce;
        }

        public static NonceUpdateEffectProcessor GetProcessor(ulong apex, Account account, ulong currentNonce, ulong newNonce)
        {
            return GetProcessor(
                new NonceUpdateEffect { Nonce = newNonce, PrevNonce = currentNonce, Pubkey = account.Pubkey, Apex = apex },
                account
            );
        }

        public static NonceUpdateEffectProcessor GetProcessor(NonceUpdateEffect effect, Account account)
        {
            return new NonceUpdateEffectProcessor(effect, account);
        }
    }
}
