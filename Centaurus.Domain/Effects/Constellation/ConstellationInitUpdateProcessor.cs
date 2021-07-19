using System;
using System.Collections.Generic;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class ConstellationInitUpdateProcessor : EffectProcessor<ConstellationUpdateEffect>
    {
        public ConstellationInitUpdateProcessor(ConstellationUpdateEffect effect)
            : base(effect)
        {
            if (Effect.Settings == null)
                throw new ArgumentNullException(nameof(Effect.Settings));
        }

        public override void CommitEffect()
        {
        }

        public override void RevertEffect()
        {
            if (Effect.PrevSettings == null)
                throw new InvalidOperationException("Current effects is init effect.");
        }
    }
}
