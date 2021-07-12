using Centaurus.Domain.Models;
using Centaurus.Domain.Quanta.Contexts;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderCancellationProcessor : QuantumProcessorBase<OrderCancellationProcessorContext>
    {
        public OrderCancellationProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override MessageTypes SupportedMessageType => MessageTypes.OrderCancellationRequest;

        public override ProcessorContext GetContext(MessageEnvelope messageEnvelope, AccountWrapper account)
        {
            return new OrderCancellationProcessorContext(Context, messageEnvelope, account);
        }

        public override Task<QuantumResultMessage> Process(OrderCancellationProcessorContext context)
        {
            var quantum = (RequestQuantum)context.QuantumEnvelope.Message;

            context.UpdateNonce();

            context.CentaurusContext.Exchange.RemoveOrder(context, context.Orderbook, context.OrderWrapper);

            var resultMessage = context.QuantumEnvelope.CreateResult(ResultStatusCodes.Success);
            return Task.FromResult((QuantumResultMessage)resultMessage);
        }


        public override Task Validate(OrderCancellationProcessorContext context)
        {
            context.ValidateNonce();

            var quantum = context.QuantumEnvelope.Message as RequestQuantum;
            var orderRequest = (OrderCancellationRequest)quantum.RequestMessage;

            context.OrderWrapper = context.CentaurusContext.Exchange.OrderMap.GetOrder(orderRequest.OrderId);
            if (context.OrderWrapper == null)
                throw new BadRequestException($"Order {orderRequest.OrderId} is not found.");

            context.Orderbook = context.CentaurusContext.Exchange.GetOrderbook(context.OrderWrapper.Order.Asset, context.OrderWrapper.Order.Side);

            //TODO: check that lot size is greater than minimum allowed lot
            if (context.OrderWrapper.AccountWrapper.Account.Id != orderRequest.Account)
                throw new ForbiddenException();

            if (context.OrderWrapper.Order.Side == OrderSide.Buy)
            {
                var balance = context.SourceAccount.Account.GetBalance(context.CentaurusContext.Constellation.GetBaseAsset());
                if (balance.Liabilities < context.OrderWrapper.Order.QuoteAmount)
                    throw new BadRequestException("Quote liabilities is less than order size.");
            }
            else
            {
                var balance = context.SourceAccount.Account.GetBalance(context.OrderWrapper.Order.Asset);
                if (balance == null)
                    throw new BadRequestException("Balance for asset not found.");
                if (balance.Liabilities < context.OrderWrapper.Order.Amount)
                    throw new BadRequestException("Asset liabilities is less than order size.");
            }

            return Task.CompletedTask;
        }
    }
}
