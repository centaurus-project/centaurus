using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ConstellationUpdateProcessor : QuantumProcessor
    {
        public ConstellationUpdateProcessor(ExecutionContext context)
            : base(context)
        {

        }

        public override MessageTypes SupportedMessageType => MessageTypes.ConstellationUpdate;

        public override Task<QuantumResultMessage> Process(ProcessorContext context)
        {
            var updateQuantum = (ConstellationUpdate)((ConstellationQuantum)context.QuantumEnvelope.Message).RequestMessage;

            var settings = BuildSettings(updateQuantum);

            context.AddConstellationUpdate(settings, Context.Constellation);


            var updateSnapshot = PersistenceManager.GetSnapshot(
                context.Apex,
                settings,
                Context.AccountStorage?.GetAll().ToList() ?? new List<AccountWrapper>(),
                Context.Exchange?.OrderMap.GetAllOrders().ToList() ?? new List<OrderWrapper>(),
                GetCursors(settings.Providers),
                context.QuantumEnvelope.ComputeMessageHash()
            );
            context.CentaurusContext.Setup(updateSnapshot);

            if (context.CentaurusContext.AppState.State == State.WaitingForInit)
            {
                context.CentaurusContext.AppState.SetState(State.Running);
                if (!context.CentaurusContext.IsAlpha) //set auditor to Ready state after init
                {
                    //send new apex cursor message to notify Alpha that the auditor was initialized
                    context.CentaurusContext.OutgoingMessageStorage.EnqueueMessage(new QuantaBatchRequest { LastKnownApex = context.Apex });
                    context.CentaurusContext.AppState.SetState(State.Ready);
                }
            }

            return Task.FromResult((QuantumResultMessage)context.QuantumEnvelope.CreateResult(ResultStatusCodes.Success));
        }

        private Dictionary<string, string> GetCursors(List<ProviderSettings> providers)
        {
            var cursors = new Dictionary<string, string>();
            foreach (var provider in providers)
            {
                var cursor = provider.InitCursor;
                var currentProvider = default(PaymentProvider.PaymentProviderBase);
                var providerId = PaymentProviderBase.GetProviderId(provider.Provider, provider.Name);
                if (Context.PaymentProvidersManager?.TryGetManager(providerId, out currentProvider) ?? false
                    && currentProvider.CompareCursors(currentProvider.Cursor, provider.InitCursor) > 0)
                    cursor = currentProvider.Cursor;
                cursors.Add(providerId, cursor);
            }
            return cursors;
        }

        private ConstellationSettings BuildSettings(ConstellationUpdate constellationUpdate)
        {
            return new ConstellationSettings
            {
                Assets = constellationUpdate.Assets,
                Auditors = constellationUpdate.Auditors,
                MinAccountBalance = constellationUpdate.MinAccountBalance,
                MinAllowedLotSize = constellationUpdate.MinAllowedLotSize,
                Providers = constellationUpdate.Providers,
                RequestRateLimits = constellationUpdate.RequestRateLimits
            };
        }

        const int minAuditorsCount = 2;

        public override Task Validate(ProcessorContext context)
        {
            if (context.CentaurusContext.AppState.State != State.WaitingForInit)
                throw new InvalidOperationException("Init quantum can be handled only when application is in WaitingForInit state.");

            var requestEnvelope = ((ConstellationQuantum)context.QuantumEnvelope.Message).RequestEnvelope;

            if (!(requestEnvelope.Message is ConstellationUpdate constellationUpdate))
                throw new ArgumentException("Message is not ConstellationInitRequest");

            if (constellationUpdate.Auditors == null || constellationUpdate.Auditors.Count() < minAuditorsCount)
                throw new ArgumentException($"Min auditors count is {minAuditorsCount}");

            if (!constellationUpdate.Auditors.All(a => requestEnvelope.IsSignedBy(a.PubKey)))
                throw new InvalidOperationException("The quantum should be signed by all auditors.");

            if (!requestEnvelope.AreSignaturesValid())
                throw new InvalidOperationException("The quantum's signatures are invalid.");

            if (constellationUpdate.MinAccountBalance < 1)
                throw new ArgumentException("Minimal account balance is less then 0");

            if (constellationUpdate.MinAllowedLotSize < 1)
                throw new ArgumentException("Minimal allowed lot size is less then 0");

            if (constellationUpdate.Assets.GroupBy(a => a.Code).Any(g => g.Count() > 1) || constellationUpdate.Assets.GroupBy(a => a.Code).Any(g => g.Count() > 1))
                throw new ArgumentException("All asset values must be unique");

            if (constellationUpdate.Assets.Any(a => a.Code.Length > 4))
                throw new Exception("Asset code should not exceed 4 bytes");

            if (constellationUpdate.RequestRateLimits == null || constellationUpdate.RequestRateLimits.HourLimit < 1 || constellationUpdate.RequestRateLimits.MinuteLimit < 1)
                throw new ArgumentException("Request rate limit values should be greater than 0");

            return Task.CompletedTask;
        }
    }
}
