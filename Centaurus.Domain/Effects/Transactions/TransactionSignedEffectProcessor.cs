using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TransactionSignedEffectProcessor : EffectProcessor<TransactionSignedEffect>
    {
        public TransactionSignedEffectProcessor(TransactionSignedEffect effect)
            :base(effect)
        {

        }

        public override void CommitEffect()
        {
        }

        public override void RevertEffect()
        {
        }
    }
}
