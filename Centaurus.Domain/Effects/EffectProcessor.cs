using Centaurus.Domain.Models;
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
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
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

    public abstract class ClientEffectProcessor<T> : EffectProcessor<T>
        where T : Effect
    {
        public ClientEffectProcessor(T effect, AccountWrapper accountWrapper)
            :base(effect)
        {
            AccountWrapper = accountWrapper ?? throw new ArgumentNullException(nameof(accountWrapper));
        }

        public AccountWrapper AccountWrapper { get; }
    }
}
