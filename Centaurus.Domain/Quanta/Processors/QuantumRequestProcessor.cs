using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class QuantumRequestProcessor<T> : IQuantumRequestProcessor<T>, IQuantumRequestProcessor
        where T : ProcessorContext
    {
        public abstract MessageTypes SupportedMessageType { get; }

        public abstract Task<ResultMessage> Process(T context);

        public abstract Task Validate(T context);

        Task IQuantumRequestProcessor.Validate(object context)
        {
            return Validate((T)context);
        }

        Task<ResultMessage> IQuantumRequestProcessor.Process(object context)
        {
            return Process((T)context);
        }

        public abstract T GetContext(EffectProcessorsContainer container);

        ProcessorContext IQuantumRequestProcessor.GetContext(EffectProcessorsContainer container) => GetContext(container);

        public Dictionary<int, Message> GetNotificationMessages(T context)
        {
            var requestAccount = 0;
            if (context.Envelope.Message is RequestQuantum request)
                requestAccount = request.RequestMessage.Account;

            var result = new Dictionary<int, EffectsNotification>();
            var effects = context.EffectProcessors.Effects;
            foreach (var effect in effects)
            {
                if (effect.Account == 0
                    || effect.Account == requestAccount)
                    continue;
                if (!result.TryGetValue(effect.Account, out var effectsNotification))
                {
                    effectsNotification = new EffectsNotification { Effects = new List<Effect>() };
                    result.Add(effect.Account, effectsNotification);
                }
                effectsNotification.Effects.Add(effect);
            }
            return result.ToDictionary(k => k.Key, v => (Message)v.Value);
        }

        public Dictionary<int, Message> GetNotificationMessages(object context)
        {
            return GetNotificationMessages((T)context);
        }
    }

    public abstract class QuantumRequestProcessor : QuantumRequestProcessor<ProcessorContext>
    {
        public override ProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new ProcessorContext(container);
        }
    }
}
