using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var initQuantum = (ConstellationInitQuantum)context.Envelope.Message;

            var effect = new ConstellationInitEffect
            {
                Apex = initQuantum.Apex,
                Assets = initQuantum.Assets,
                Auditors = initQuantum.Auditors,
                MinAccountBalance = initQuantum.MinAccountBalance,
                MinAllowedLotSize = initQuantum.MinAllowedLotSize,
                Vault = initQuantum.Vault,
                RequestRateLimits = initQuantum.RequestRateLimits
            };

            context.EffectProcessors.Add(new ConstellationInitEffectProcessor(effect));
            var initSnapshot = PersistenceManager.GetSnapshot(effect, context.Envelope.ComputeHash());
            await Global.Setup(initSnapshot);

            Global.AppState.State = ApplicationState.Running;
            if (!Global.IsAlpha) //set auditor to Ready state after init
            {
                //send new apex cursor message to notify Alpha that the auditor was initialized
                OutgoingMessageStorage.EnqueueMessage(new SetApexCursor { Apex = 1 });
                TxListener.RegisterListener(initSnapshot.TxCursor);
                Global.AppState.State = ApplicationState.Ready;
            }

            return context.Envelope.CreateResult(ResultStatusCodes.Success, context.EffectProcessors.Effects);
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
