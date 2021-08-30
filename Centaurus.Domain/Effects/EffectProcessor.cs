using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface IEffectProcessor
    {
        public Effect Effect { get; }

        public void CommitEffect();
        public void RevertEffect();
    }

    public interface IEffectProcessor<out T>: IEffectProcessor
        where T : Effect
    {
        public new T Effect { get; }
    }

    public abstract class EffectProcessor : IEffectProcessor
    {
        public EffectProcessor(Effect effect)
        {
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        public Effect Effect { get; }

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

    public abstract class EffectProcessor<T>: EffectProcessor, IEffectProcessor<T>
        where T: Effect
    {
        public EffectProcessor(T effect)
            :base(effect)
        {
        }

        public new T Effect => (T)base.Effect;

        Effect IEffectProcessor.Effect => Effect;
    }

    public abstract class AccountEffectProcessor : EffectProcessor
    {
        public AccountEffectProcessor(AccountEffect clientEffect, Account account)
            :base(clientEffect)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account));
        }

        public new AccountEffect Effect => (AccountEffect)base.Effect;

        public Account Account { get; }
    }

    public abstract class ClientEffectProcessor<T> : AccountEffectProcessor, IEffectProcessor<T>
        where T : AccountEffect
    {
        public ClientEffectProcessor(T effect, Account account)
            :base(effect, account)
        {
        }

        public new T Effect => (T)base.Effect;
    }
}
