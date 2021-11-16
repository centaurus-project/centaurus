using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderCancellationProcessor : QuantumProcessorBase
    {
        public OrderCancellationProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(OrderCancellationRequest).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem quantumProcessingItem)
        {
            UpdateNonce(quantumProcessingItem);

            Context.Exchange.RemoveOrder(quantumProcessingItem, Context.Constellation.QuoteAsset.Code);

            var resultMessage = quantumProcessingItem.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success);
            return Task.FromResult((QuantumResultMessageBase)resultMessage);
        }

        public override Task Validate(QuantumProcessingItem quantumProcessingItem)
        {
            ValidateNonce(quantumProcessingItem);

            var quantum = (RequestQuantum)quantumProcessingItem.Quantum;
            var orderRequest = (OrderCancellationRequest)quantum.RequestMessage;

            var orderWrapper = Context.Exchange.OrderMap.GetOrder(orderRequest.OrderId);
            if (orderWrapper == null)
                throw new BadRequestException($"Order {orderRequest.OrderId} is not found.");

            if (!orderWrapper.Account.Pubkey.Equals(orderRequest.Account))
                throw new ForbiddenException();

            return Task.CompletedTask;
        }
    }
}
