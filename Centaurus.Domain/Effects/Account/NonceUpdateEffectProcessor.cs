using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Centaurus.Domain
{
    public class NonceUpdateEffectProcessor : BaseAccountEffectProcessor<NonceUpdateEffect>
    {
        public NonceUpdateEffectProcessor(NonceUpdateEffect effect)
            : base(effect)
        {
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            Account.Nonce = Effect.Nonce;
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            Account.Nonce = Effect.PrevNonce;
        }
    }
}
