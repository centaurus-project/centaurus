using Centaurus.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class InitQuantumProcessor : QuantumProcessor
    {
        public override MessageTypes SupportedMessageType => MessageTypes.ConstellationInitRequest;

        public override async Task<QuantumResultMessage> Process(ProcessorContext context)
        {
            var initQuantum = (ConstellationInitRequest)((ConstellationQuantum)context.Envelope.Message).RequestMessage;

            context.EffectProcessors.AddConstellationInit(initQuantum);
            var initSnapshot = PersistenceManager.GetSnapshot(
                (ConstellationInitEffect)context.EffectProcessors.Effects[0],
                context.Envelope.ComputeMessageHash()
            );
            await context.CentaurusContext.Setup(initSnapshot);

            context.CentaurusContext.AppState.State = ApplicationState.Running;
            if (!context.CentaurusContext.IsAlpha) //set auditor to Ready state after init
            {
                //send new apex cursor message to notify Alpha that the auditor was initialized
                context.CentaurusContext.OutgoingMessageStorage.EnqueueMessage(new SetApexCursor { Apex = 1 });
                context.CentaurusContext.AppState.State = ApplicationState.Ready;
            }

            return (QuantumResultMessage)context.Envelope.CreateResult(ResultStatusCodes.Success);
        }

        const int minAuditorsCount = 2;

        public override Task Validate(ProcessorContext context)
        {
            if (context.CentaurusContext.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("Init quantum can be handled only when application is in WaitingForInit state.");

            var requestEnvelope = ((ConstellationQuantum)context.Envelope.Message).RequestEnvelope;

            if (!(requestEnvelope.Message is ConstellationInitRequest constellationInit))
                throw new ArgumentException("Message is not ConstellationInitRequest");

            if (constellationInit.Auditors == null || constellationInit.Auditors.Count() < minAuditorsCount)
                throw new ArgumentException($"Min auditors count is {minAuditorsCount}");

            if (!constellationInit.Auditors.All(a => requestEnvelope.IsSignedBy((KeyPair)a)))
                throw new InvalidOperationException("The quantum should be signed by all auditors.");

            if (!requestEnvelope.AreSignaturesValid())
                throw new InvalidOperationException("The quantum's signatures are invalid.");

            if (constellationInit.MinAccountBalance < 1)
                throw new ArgumentException("Minimal account balance is less then 0");

            if (constellationInit.MinAllowedLotSize < 1)
                throw new ArgumentException("Minimal allowed lot size is less then 0");

            if (constellationInit.Assets.GroupBy(a => a.ToString()).Any(g => g.Count() > 1))
                throw new ArgumentException("All asset values should be unique");

            if (constellationInit.RequestRateLimits == null || constellationInit.RequestRateLimits.HourLimit < 1 || constellationInit.RequestRateLimits.MinuteLimit < 1)
                throw new ArgumentException("Request rate limit values should be greater than 0");

            return Task.CompletedTask;
        }
    }
}
