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

            return null;
        }

        public Task Validate(MessageEnvelope envelope)
        {
            if (Global.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("Init quantum can be handled only when application is in WaitingForInit state.");

            if (Global.IsAlpha && !envelope.IsSignedBy(Global.Settings.KeyPair.PublicKey)
                || !Global.IsAlpha && !envelope.IsSignedBy(((AuditorSettings)Global.Settings).AlphaKeyPair.PublicKey))
                throw new InvalidOperationException("The quantum isn't signed by Alpha.");

            if (!envelope.AreSignaturesValid())
                throw new InvalidOperationException("The quantum's signatures are invalid.");

            return Task.CompletedTask;
        }
    }
}
