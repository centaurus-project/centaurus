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

        public EffectProcessorsContainer(MessageEnvelope quantum, DiffObject pendingDiffObject)
        {
            Envelope = quantum ?? throw new ArgumentNullException(nameof(quantum));
            PendingDiffObject = pendingDiffObject ?? throw new ArgumentNullException(nameof(pendingDiffObject));
            QuantumModel = new QuantumItem(Apex);
        }

        public MessageEnvelope Envelope { get; }

        public List<Effect> Effects { get; } = new List<Effect>();

        public Dictionary<int, EffectsModel> EffectsModels { get; } = new Dictionary<int, EffectsModel>();

        public DiffObject PendingDiffObject { get; }

        public QuantumItem QuantumModel { get; }

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
        }

        /// <summary>
        /// Unwraps and returns effects for specified account.
        /// </summary>
        /// <returns></returns>
        public Effect[] GetEffects(int account)
        {
            return Effects
                .Where(e => e.AccountWrapper?.Account.Id == account)
                .ToArray();
        }

        /// <summary>
        /// Sends envelope and all effects to specified callback
        /// </summary>
        public void Complete()
        {
            QuantumModel.Complete(QuantumModelExtensions.FromQuantum(Envelope));
            PendingDiffObject.Quanta.Add(QuantumModel);
        }
    }
}
