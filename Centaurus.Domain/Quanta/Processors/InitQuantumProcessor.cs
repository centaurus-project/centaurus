using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class InitQuantumProcessor : QuantumRequestProcessor
    {
        public override MessageTypes SupportedMessageType => MessageTypes.ConstellationInitQuantum;

        public override async Task<ResultMessage> Process(ProcessorContext context)
        {
            var initEffects = await SnapshotManager.ApplyInitUpdates(context.Envelope);

            var snapshot = await SnapshotManager.GetSnapshot();

            Global.Setup(snapshot);

            Global.AppState.State = ApplicationState.Running;
            if (!Global.IsAlpha) //set auditor to Ready state after init
            {
                Global.AppState.State = ApplicationState.Ready;
                //send new apex cursor message to notify Alpha that the auditor was initialized
                OutgoingMessageStorage.EnqueueMessage(new SetApexCursor { Apex = 1 });
            }

            return context.Envelope.CreateResult(ResultStatusCodes.Success, new List<Effect>(initEffects));
        }

        public override Task Validate(ProcessorContext context)
        {
            if (Global.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("Init quantum can be handled only when application is in WaitingForInit state.");

            if (!Global.IsAlpha && !context.Envelope.IsSignedBy(((AuditorSettings)Global.Settings).AlphaKeyPair.PublicKey))
                throw new InvalidOperationException("The quantum isn't signed by Alpha.");

            if (!context.Envelope.AreSignaturesValid())
                throw new InvalidOperationException("The quantum's signatures are invalid.");

            return Task.CompletedTask;
        }
    }
}
