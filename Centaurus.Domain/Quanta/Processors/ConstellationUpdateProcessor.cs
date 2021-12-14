using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ConstellationUpdateProcessor : QuantumProcessorBase
    {
        public ConstellationUpdateProcessor(ExecutionContext context)
            : base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(ConstellationUpdate).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem processingItem)
        {
            var updateQuantum = (ConstellationUpdate)((ConstellationQuantum)processingItem.Quantum).RequestMessage;

            var settings = updateQuantum.ToConstellationSettings(processingItem.Apex);

            processingItem.AddConstellationUpdate(settings, Context.ConstellationSettingsManager.Current);

            var updateSnapshot = settings.ToSnapshot(
                processingItem.Apex,
                Context.AccountStorage?.GetAll().ToList() ?? new List<Account>(),
                Context.Exchange?.OrderMap.GetAllOrders().ToList() ?? new List<OrderWrapper>(),
                GetCursors(settings.Providers),
                processingItem.Quantum.ComputeHash()
            );
            Context.Init(updateSnapshot);

            var currentNode = Context.NodesManager.CurrentNode;
            //if state is undefined, than we need to init it
            if (currentNode.State == State.WaitingForInit)
                currentNode.Init(State.Running);

            return Task.FromResult((QuantumResultMessageBase)processingItem.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success));
        }

        private Dictionary<string, string> GetCursors(List<ProviderSettings> providers)
        {
            var cursors = new Dictionary<string, string>();
            foreach (var provider in providers)
            {
                var cursor = provider.InitCursor;
                var currentProvider = default(PaymentProviderBase);
                var providerId = PaymentProviderBase.GetProviderId(provider.Provider, provider.Name);
                if (Context.PaymentProvidersManager?.TryGetManager(providerId, out currentProvider) ?? false
                    && currentProvider.CompareCursors(currentProvider.Cursor, provider.InitCursor) > 0)
                    cursor = currentProvider.Cursor;
                cursors.Add(providerId, cursor);
            }
            return cursors;
        }

        const int minAuditorsCount = 2;

        public override Task Validate(QuantumProcessingItem processingItem)
        {
            ((ConstellationQuantum)processingItem.Quantum).Validate(Context);

            var requestEnvelope = ((ConstellationQuantum)processingItem.Quantum).RequestEnvelope;

            if (!(requestEnvelope.Message is ConstellationUpdate constellationUpdate))
                throw new ArgumentException("Message is not ConstellationUpdate");

            if (constellationUpdate.Auditors == null || constellationUpdate.Auditors.Count() < minAuditorsCount)
                throw new ArgumentException($"Min auditors count is {minAuditorsCount}");

            if (constellationUpdate.LastUpdateApex != (Context.ConstellationSettingsManager.Current?.Apex ?? 0ul))
                throw new InvalidOperationException($"Last update apex is invalid.");

            if (!constellationUpdate.Auditors.All(a => a.Address == null || Uri.TryCreate($"http://{a.Address}", UriKind.Absolute, out _)))
                throw new InvalidOperationException("At least one auditor's address is invalid.");

            if (constellationUpdate.Alpha == null
                || !constellationUpdate.Auditors.Any(a => a.PubKey.Equals(constellationUpdate.Alpha) && a.Address != null)) //if address is null than it's not full node and it cannot become Alpha
                throw new InvalidOperationException("Alpha should be one of the full node auditors.");

            if (constellationUpdate.MinAccountBalance < 1)
                throw new ArgumentException("Minimal account balance is less then 0");

            if (constellationUpdate.MinAllowedLotSize < 1)
                throw new ArgumentException("Minimal allowed lot size is less then 0");

            if (constellationUpdate.Assets.GroupBy(a => a.Code).Any(g => g.Count() > 1) || constellationUpdate.Assets.GroupBy(a => a.Code).Any(g => g.Count() > 1))
                throw new ArgumentException("All asset values must be unique");

            if (constellationUpdate.Assets.Count(a => a.IsQuoteAsset) != 1)
                throw new ArgumentException("ConstellationSettingsManager.Current must contain one quote asset.");

            if (constellationUpdate.Assets.Any(a => a.Code.Length > 4))
                throw new Exception("Asset code should not exceed 4 bytes");

            if (constellationUpdate.RequestRateLimits == null || constellationUpdate.RequestRateLimits.HourLimit < 1 || constellationUpdate.RequestRateLimits.MinuteLimit < 1)
                throw new ArgumentException("Request rate limit values should be greater than 0");

            return Task.CompletedTask;
        }
    }
}
