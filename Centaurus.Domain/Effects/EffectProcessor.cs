using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface IEffectProcessor<out T>
        where T : Effect
    {
        public T Effect { get; }

        public UpdatedObject[] CommitEffect();
        public void RevertEffect();
    }

    public abstract class EffectProcessor<T>: IEffectProcessor<T>
        where T: Effect
    {
        public EffectProcessor(T effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            Effect = effect;
        }

        public T Effect { get; }

        public abstract UpdatedObject[] CommitEffect();
        public abstract void RevertEffect();
    }
}
