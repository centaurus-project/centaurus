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

        object IQuantumRequestProcessor.GetContext(EffectProcessorsContainer container) => GetContext(container);

        public Dictionary<RawPubKey, Message> GetNotificationMessages(T context)
        {
            RawPubKey requestAccount;
            if (context.Envelope.Message is RequestQuantum request)
                requestAccount = request.RequestMessage.Account;
            else
                requestAccount = new RawPubKey { Data = new byte[32] };

            var result = new Dictionary<byte[], EffectsNotification>(ByteArrayComparer.Default);
            var effects = context.EffectProcessors.GetEffects();
            foreach (var effect in effects)
            {
                if (effect.Pubkey == null 
                    || effect.Pubkey.Equals(requestAccount) 
                    || Global.Constellation.Auditors.Any(a => a.Equals(effect.Pubkey)))
                    continue;
                if (!result.ContainsKey(effect.Pubkey.Data))
                    result[effect.Pubkey] = new EffectsNotification { Effects = new List<Effect>() };
                result[effect.Pubkey].Effects.Add(effect);
            }
            return result.ToDictionary(k => (RawPubKey)k.Key, v => (Message)v.Value);
        }

        public Dictionary<RawPubKey, Message> GetNotificationMessages(object context)
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
