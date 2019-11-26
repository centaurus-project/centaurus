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

        public EffectProcessorsContainer(PendingUpdates pendingUpdates, MessageEnvelope quantum)
        {
            this.pendingUpdates = pendingUpdates ?? throw new ArgumentNullException(nameof(pendingUpdates));
            this.quantum = quantum ?? throw new ArgumentNullException(nameof(quantum));
        }

        private PendingUpdates pendingUpdates;
        private MessageEnvelope quantum;

        public void Add(IEffectProcessor<Effect> effect)
        {
            container.Add(effect);
        }

        public Effect[] GetEffects()
        {
            return container.Select(ep => ep.Effect).ToArray();
        }

        public void Commit()
        {
            var effectsLength = container.Count;
            for (var i = 0; i < effectsLength; i++)
            {
                var currentEffect = container[i];
                currentEffect.CommitEffect();
            }
            pendingUpdates.Add(quantum, GetEffects());
        }

        public void Revert()
        {
            //TODO: revert the PendingUpdates object state
            for (var i = container.Count - 1; i >= 0; i--)
            {
                var currentEffect = container[i];
                currentEffect.RevertEffect();
            }
        }
    }
}
