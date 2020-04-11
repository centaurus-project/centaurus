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

        public void CommitEffect();
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

        public bool IsProcessed { get; private set; }

        protected void MarkAsProcessed()
        {
            if (IsProcessed)
                throw new Exception("Effect is processed already.");
            IsProcessed = true;
        }

        public abstract void CommitEffect();

        public abstract void RevertEffect();
    }
}
