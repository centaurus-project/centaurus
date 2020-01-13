using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class InitQuantumProcessor : IQuantumRequestProcessor
    {
        public MessageTypes SupportedMessageType => MessageTypes.ConstellationInitQuantum;

        public async Task<ResultMessage> Process(MessageEnvelope envelope)
        {
            await SnapshotManager.ApplyInitUpdates(envelope);

            var snapshot = await SnapshotManager.GetSnapshot();

            Global.Setup(snapshot);

            Global.AppState.State = ApplicationState.Running;
            if (!Global.IsAlpha) //set auditor to Ready state after init
            {
                Global.AppState.State = ApplicationState.Ready;
                //send new apex cursor message to notify Alpha that the auditor was initialized
                OutgoingMessageStorage.EnqueueMessage(new SetApexCursor { Apex = 1 });
            }

            return null;
        }

        public Task Validate(MessageEnvelope envelope)
        {
            if (Global.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("Init quantum can be handled only when application is in WaitingForInit state.");

            if (!Global.IsAlpha && !envelope.IsSignedBy(((AuditorSettings)Global.Settings).AlphaKeyPair.PublicKey))
                throw new InvalidOperationException("The quantum isn't signed by Alpha.");

            if (!envelope.AreSignaturesValid())
                throw new InvalidOperationException("The quantum's signatures are invalid.");

            return Task.CompletedTask;
        }
    }
}
