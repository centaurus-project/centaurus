using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class NonceUpdateEffectProcessor : BaseAccountEffectProcessor<NonceUpdateEffect>
    {
        private Account account;

        public NonceUpdateEffectProcessor(NonceUpdateEffect effect, AccountStorage accountStorage)
            : base(effect, accountStorage)
        {
        }

        public override void CommitEffect()
        {
            Account.Nonce = Effect.Nonce;
        }

        public override void RevertEffect()
        {
            Account.Nonce = Effect.PrevNonce;
        }
    }
}
