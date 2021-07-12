using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    /// <summary>
    /// In some cases we have a lot of duplicate actions on validation and processing envelopes. 
    /// For optimization, we need a context to store already resulted data.
    /// </summary>
    public class ProcessorContext : ContextualBase
    {
        public ProcessorContext(ExecutionContext context, MessageEnvelope quantum, AccountWrapper account)
            : base(context)
        {
            QuantumEnvelope = quantum ?? throw new ArgumentNullException(nameof(quantum));
            SourceAccount = account;
        }

        private readonly List<Effect> effects = new List<Effect>();

        public ExecutionContext CentaurusContext => Context;

        public MessageEnvelope QuantumEnvelope { get; }

        public AccountWrapper SourceAccount { get; }

        public Quantum Quantum => (Quantum)QuantumEnvelope.Message;

        public ulong Apex => Quantum.Apex;

        public bool IsCompleted => EffectsProof != null;

        public EffectsProof EffectsProof { get; private set; }

        public byte[] EffectsHash { get; private set; }

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effectProcessor"></param>
        public void AddEffectProcessor(IEffectProcessor<Effect> effectProcessor)
        {
            if (IsCompleted)
                throw new InvalidOperationException("The quantum already processed.");
            effects.Add(effectProcessor.Effect);
            effectProcessor.CommitEffect();
        }

        public void Complete(byte[] buffer = null)
        {
            EffectsProof = new EffectsProof
            {
                Hashes = new EffectHashes
                {
                    Hashes = effects.Select(e => new Hash { Data = e.ComputeHash(buffer) }).ToList()
                },
                Signatures = new List<Ed25519Signature>()
            };

            EffectsHash = EffectsProof.Hashes.ComputeHash(buffer);
        }

        public void PersistQuantum()
        {
            if (!IsCompleted)
                throw new InvalidOperationException("The quantum must be processed before persistence.");
            Context.PendingUpdatesManager.AddQuantum(Apex, QuantumEnvelope, effects, EffectsProof);
        }

        public List<Effect> GetClientEffects()
        {
            if (QuantumEnvelope.Message is RequestQuantum request)
                return effects.Where(e => e.Account == request.RequestMessage.Account).ToList();
            return effects;
        }

        /// <summary>
        /// Creates message notifications for accounts that were affected by quantum
        /// </summary>
        /// <returns></returns>
        public Dictionary<ulong, Message> GetNotificationMessages()
        {
            var requestAccount = 0ul;
            if (QuantumEnvelope.Message is RequestQuantum request)
                requestAccount = request.RequestMessage.Account;

            var result = new Dictionary<ulong, EffectsNotification>();
            foreach (var effect in effects)
            {
                if (effect.Account == 0 || effect.Account == requestAccount)
                    continue;
                if (!result.TryGetValue(effect.Account, out var effectsNotification))
                {
                    effectsNotification = new EffectsNotification { ClientEffects = new List<Effect>() };
                    result.Add(effect.Account, effectsNotification);
                }
                effectsNotification.ClientEffects.Add(effect);
            }
            return result.ToDictionary(k => k.Key, v => (Message)v.Value);
        }
    }
}
