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

        public override void CommitEffect()
        {
            account.Nonce = Effect.Nonce;
        }

        public override void RevertEffect()
        {
            account.Nonce = Effect.PrevNonce;
        }

        public static NonceUpdateEffectProcessor GetProcessor(ulong apex, Account account, ulong currentNonce, ulong newNonce)
        {
            return new NonceUpdateEffectProcessor(
                new NonceUpdateEffect { Nonce = newNonce, PrevNonce = currentNonce, Pubkey = account.Pubkey, Apex = apex },
                account
            );
        }
    }
}
