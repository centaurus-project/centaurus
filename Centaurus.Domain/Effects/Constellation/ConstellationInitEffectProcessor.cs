using System;
using System.Collections.Generic;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class ConstellationInitEffectProcessor : EffectProcessor<ConstellationInitEffect>
    {
        public ConstellationInitEffectProcessor(ConstellationInitEffect effect)
            : base(effect)
        {
        }

        public override void CommitEffect()
        {
        }

        public override void RevertEffect()
        {
            throw new NotImplementedException();
        }
    }
}
