using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AlphaUpdateProcessor : QuantumProcessorBase
    {
        public AlphaUpdateProcessor(ExecutionContext context)
            : base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(AlphaUpdate).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem processingItem)
        {
            var alphaUpdate = (AlphaUpdate)((ConstellationQuantum)processingItem.Quantum).RequestMessage;

            //make copy of current settings
            var newConstellationSettings = XdrConverter.Deserialize<ConstellationSettings>(XdrConverter.Serialize(Context.Constellation));

            newConstellationSettings.Apex = processingItem.Apex;
            newConstellationSettings.Alpha = alphaUpdate.Alpha;

            processingItem.AddConstellationUpdate(Context.Constellation, Context.Constellation);

            Context.UpdateConstellationSettings(newConstellationSettings);

            return Task.FromResult((QuantumResultMessageBase)processingItem.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success));
        }

        public override Task Validate(QuantumProcessingItem processingItem)
        {
            var currentState = Context.NodesManager.CurrentNode.State;
            if (currentState == State.Undefined || currentState == State.WaitingForInit)
                throw new InvalidOperationException($"Constellation is not initialized yet.");

            ((ConstellationQuantum)processingItem.Quantum).Validate(Context);

            var alphaUpdate = (AlphaUpdate)((ConstellationQuantum)processingItem.Quantum).RequestMessage;

            if (alphaUpdate.Alpha.Equals(Context.Constellation.Alpha))
                throw new InvalidOperationException($"{Context.Constellation.Alpha.GetAccountId()} is Alpha already.");

            if (!Context.Constellation.Auditors.Any(a => a.PubKey.Equals(Context.Constellation.Alpha)))
                throw new InvalidOperationException($"{Context.Constellation.Alpha.GetAccountId()} is not an auditor.");

            return Task.CompletedTask;
        }
    }
}
