using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class EffectProcessorsContainer
    {
        public EffectProcessorsContainer(CentaurusContext context, MessageEnvelope quantum, DiffObject pendingDiffObject)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Envelope = quantum ?? throw new ArgumentNullException(nameof(quantum));
            PendingDiffObject = pendingDiffObject ?? throw new ArgumentNullException(nameof(pendingDiffObject));
        }

        public CentaurusContext Context { get; }

        public MessageEnvelope Envelope { get; }

        public List<Effect> Effects { get; } = new List<Effect>();


        public HashSet<int> AffectedAccounts = new HashSet<int>();

        public DiffObject PendingDiffObject { get; }

        public Quantum Quantum => (Quantum)Envelope.Message;

        public long Apex => Quantum.Apex;

        public RequestQuantum RequestQuantum => (RequestQuantum)Envelope.Message;

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effectProcessor"></param>
        public void Add(IEffectProcessor<Effect> effectProcessor)
        {
            Effects.Add(effectProcessor.Effect);
            effectProcessor.CommitEffect();
            this.Aggregate(Envelope, effectProcessor.Effect, Effects.Count - 1);
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
        public void Complete(byte[] buffer)
        {
            var quantumModel = QuantumContainerExtensions.FromQuantumContainer(
                Envelope,
                Effects, 
                AffectedAccounts.ToArray(), 
                buffer);
            PendingDiffObject.Quanta.Add(quantumModel);
            PendingDiffObject.EffectsCount += Effects.Count;
        }
    }
}
