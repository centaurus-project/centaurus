using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class EffectProcessorsContainer
    {
        List<IEffectProcessor<Effect>> container = new List<IEffectProcessor<Effect>>();

        public void Add(IEffectProcessor<Effect> effect)
        {
            container.Add(effect);
        }

        public IEnumerable<Effect> GetEffects()
        {
            return container.Select(ep => ep.Effect);
        }

        public void Commit()
        {
            for (var i = 0; i < container.Count; i++)
            {
                var currentEffect = container[i];
                currentEffect.CommitEffect();
            }
        }
    }
}
