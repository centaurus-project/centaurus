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
            this.pendingDiffObject = pendingDiffObject ?? throw new ArgumentNullException(nameof(pendingDiffObject));
        }

        public MessageEnvelope Envelope { get; }

        public List<Effect> Effects { get; } = new List<Effect>();

        private readonly DiffObject pendingDiffObject;

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
            pendingDiffObject.Aggregate(Envelope, effectProcessor.Effect, Effects.Count - 1);
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
        public void Complete()
        {
            //TODO: find more elegant way to handle this scenario
            //we mustn't save orders that were closed immediately without adding to order-book
            if (Quantum is RequestQuantum request
                && request.RequestMessage is OrderRequest orderRequest
                && !Effects.Any(e => e.EffectType == EffectTypes.OrderPlaced))
            {
                pendingDiffObject.Orders.Remove(OrderIdConverter.FromRequest(orderRequest, Quantum.Apex));
            }
            this.pendingDiffObject.Quanta.Add(QuantumModelExtensions.FromQuantum(Envelope));
        }
    }
}
