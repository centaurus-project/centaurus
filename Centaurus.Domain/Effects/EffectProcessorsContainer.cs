using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class EffectProcessorsContainer: ContextualBase
    {
        public EffectProcessorsContainer(ExecutionContext context, MessageEnvelope quantum, DiffObject pendingDiffObject, AccountWrapper account)
            :base(context)
        {
            Envelope = quantum ?? throw new ArgumentNullException(nameof(quantum));
            PendingDiffObject = pendingDiffObject ?? throw new ArgumentNullException(nameof(pendingDiffObject));
            AccountWrapper = account;
        }

        public MessageEnvelope Envelope { get; }

        public List<Effect> Effects { get; } = new List<Effect>();

        public HashSet<int> AffectedAccounts { get; } = new HashSet<int>();

        public DiffObject PendingDiffObject { get; }

        public Quantum Quantum => (Quantum)Envelope.Message;

        public long Apex => Quantum.Apex;

        public RequestQuantum RequestQuantum => (RequestQuantum)Envelope.Message;

        public AccountWrapper AccountWrapper { get; }

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effectProcessor"></param>
        public void Add(IEffectProcessor<Effect> effectProcessor)
        {
            Effects.Add(effectProcessor.Effect);
            effectProcessor.CommitEffect();
            this.Aggregate(Envelope, effectProcessor.Effect);
            if (effectProcessor.Effect.Account != 0)
                AffectedAccounts.Add(effectProcessor.Effect.Account);
        }

        /// <summary>
        /// Unwraps and returns effects for specified account.
        /// </summary>
        /// <returns></returns>
        public Effect[] GetEffects(int account)
        {
            return Effects
                .Where(e => e.Account == account)
                .ToArray();
        }
        /// <summary>
        /// Sends envelope and all effects to specified callback
        /// </summary>
        /// <param name="buffer">Buffer to use for serialization</param>
        public void Complete(EffectsProof effectsProof, byte[] buffer)
        {
            var quantumModel = QuantumContainerExtensions.FromQuantumContainer(
                Envelope,
                Effects,
                effectsProof,
                AffectedAccounts.ToArray(), 
                buffer);
            PendingDiffObject.Quanta.Add(quantumModel);
            PendingDiffObject.EffectsCount += Effects.Count;
        }

        /// <summary>
        /// Creates message notifications for accounts that were affected by quantum
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, Message> GetNotificationMessages()
        {
            var requestAccount = 0;
            if (Envelope.Message is RequestQuantum request)
                requestAccount = request.RequestMessage.Account;

            var result = new Dictionary<int, EffectsNotification>();
            foreach (var effect in Effects)
            {
                if (effect.Account == 0
                    || effect.Account == requestAccount)
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
