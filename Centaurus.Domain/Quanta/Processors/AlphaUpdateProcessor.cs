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
    public class AlphaUpdateProcessor : ConstellationUpdateProcessorBase
    {
        public AlphaUpdateProcessor(ExecutionContext context)
            : base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(AlphaUpdate).Name;

        public override async Task<QuantumResultMessageBase> Process(QuantumProcessingItem processingItem)
        {
            var alphaUpdate = (AlphaUpdate)((ConstellationQuantum)processingItem.Quantum).RequestMessage;

            //make copy of current settings
            var newConstellationSettings = XdrConverter.Deserialize<ConstellationSettings>(XdrConverter.Serialize(Context.ConstellationSettingsManager.Current));

            newConstellationSettings.Apex = processingItem.Apex;
            newConstellationSettings.Alpha = alphaUpdate.Alpha;

            await UpdateConstellationSettings(processingItem, newConstellationSettings);

            return (QuantumResultMessageBase)processingItem.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success);
        }

        public override Task Validate(QuantumProcessingItem processingItem)
        {
            var currentState = Context.NodesManager.CurrentNode.State;
            if (currentState == State.Undefined || currentState == State.WaitingForInit)
                throw new InvalidOperationException($"ConstellationSettingsManager.Current is not initialized yet.");

            base.Validate(processingItem);

            var alphaUpdate = (AlphaUpdate)((ConstellationQuantum)processingItem.Quantum).RequestMessage;

            if (alphaUpdate.LastUpdateApex != Context.ConstellationSettingsManager.Current.Apex)
                throw new InvalidOperationException($"Last update apex is invalid.");

            if (alphaUpdate.Alpha.Equals(Context.ConstellationSettingsManager.Current.Alpha))
                throw new InvalidOperationException($"{Context.ConstellationSettingsManager.Current.Alpha.GetAccountId()} is Alpha already.");

            if (!Context.ConstellationSettingsManager.Current.Auditors.Any(a => a.PubKey.Equals(Context.ConstellationSettingsManager.Current.Alpha)))
                throw new InvalidOperationException($"{Context.ConstellationSettingsManager.Current.Alpha.GetAccountId()} is not an auditor.");

            return Task.CompletedTask;
        }
    }
}
