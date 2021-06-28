using Centaurus.Domain.Quanta.Contexts;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderCancellationProcessor : QuantumProcessor<OrderCancellationProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.OrderCancellationRequest;

        public override OrderCancellationProcessorContext GetContext(EffectProcessorsContainer effectProcessors)
        {
            return new OrderCancellationProcessorContext(effectProcessors);
        }

        public override Task<QuantumResultMessage> Process(OrderCancellationProcessorContext context)
        {
            var quantum = (RequestQuantum)context.Envelope.Message;

            context.UpdateNonce();

            context.CentaurusContext.Exchange.RemoveOrder(context.EffectProcessors, context.Orderbook, context.OrderWrapper);

            var resultMessage = context.Envelope.CreateResult(ResultStatusCodes.Success);
            return Task.FromResult((QuantumResultMessage)resultMessage);
        }


        public override Task Validate(OrderCancellationProcessorContext context)
        {
            context.ValidateNonce();

            var quantum = context.Envelope.Message as RequestQuantum;
            var orderRequest = (OrderCancellationRequest)quantum.RequestMessage;

            var orderData = OrderIdConverter.Decode(orderRequest.OrderId);
            if (!context.CentaurusContext.Exchange.HasMarket(orderData.Asset))
                throw new BadRequestException("Asset is not supported.");

            context.Orderbook = context.CentaurusContext.Exchange.GetOrderbook(orderData.Asset, orderData.Side);

            context.OrderWrapper = context.Orderbook.GetOrder(orderRequest.OrderId);

            if (context.OrderWrapper is null)
                throw new BadRequestException($"Order {orderRequest.OrderId} is not found.{(quantum.Apex != default ? $" Apex {quantum.Apex}" : "")}");

            //TODO: check that lot size is greater than minimum allowed lot
            if (context.OrderWrapper.AccountWrapper.Account.Id != orderRequest.Account)
                throw new ForbiddenException();

            if (context.OrderSide == OrderSide.Buy)
            {
                var balance = context.SourceAccount.Account.GetBalance(0);
                if (balance.Liabilities < context.OrderWrapper.Order.QuoteAmount)
                    throw new BadRequestException("Quote liabilities is less than order size.");
            }
            else
            {
                var balance = context.SourceAccount.Account.GetBalance(orderData.Asset);
                if (balance == null)
                    throw new BadRequestException("Balance for asset not found.");
                if (balance.Liabilities < context.OrderWrapper.Order.Amount)
                    throw new BadRequestException("Asset liabilities is less than order size.");
            }

            return Task.CompletedTask;
        }
    }
}
