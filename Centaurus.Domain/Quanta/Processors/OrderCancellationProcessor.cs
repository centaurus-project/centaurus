using Centaurus.Domain.Quanta.Contexts;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OrderCancellationProcessor : QuantumRequestProcessor<OrderCancellationProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.OrderCancellationRequest;

        public override OrderCancellationProcessorContext GetContext(EffectProcessorsContainer effectProcessors)
        {
            return new OrderCancellationProcessorContext(effectProcessors);
        }

        public override Task<ResultMessage> Process(OrderCancellationProcessorContext context)
        {
            var quantum = (RequestQuantum)context.Envelope.Message;
            var orderRequest = (OrderCancellationRequest)quantum.RequestMessage;

            context.UpdateNonce();

            var xmlAmount = OrderMatcher.EstimateTradedXlmAmount(context.Order.Amount, context.Order.Price);
            //lock order reserve
            if (context.OrderSide == OrderSides.Buy)
                //TODO: check this - potential rounding error with multiple trades
                context.EffectProcessors.AddUnlockLiabilities(orderRequest.AccountWrapper.Account, 0, xmlAmount);
            else
                context.EffectProcessors.AddUnlockLiabilities(orderRequest.AccountWrapper.Account, context.Asset, context.Order.Amount);

            context.EffectProcessors.AddOrderRemoved(context.Orderbook, context.Order);

            var resultMessage = context.Envelope.CreateResult(ResultStatusCodes.Success, context.EffectProcessors.GetEffects().ToList());
            return Task.FromResult(resultMessage);
        }

        //TODO: replace all system exceptions that occur on validation with our client exceptions
        public override Task Validate(OrderCancellationProcessorContext context)
        {
            context.ValidateNonce();

            var quantum = context.Envelope.Message as RequestQuantum;
            var orderRequest = (OrderCancellationRequest)quantum.RequestMessage;

            var orderData = OrderIdConverter.Decode(orderRequest.OrderId);
            if (!Global.Exchange.HasMarket(orderData.Asset))
                throw new BadRequestException("Asset is not supported.");

            context.Orderbook = Global.Exchange.GetOrderbook(orderData.Asset, orderData.Side);

            context.Order = context.Orderbook.GetOrder(orderRequest.OrderId);

            if (context.Order is null)
                throw new BadRequestException("Order is not found.");

            //check that lot size is greater than minimum allowed lot
            if (!ByteArrayPrimitives.Equals(context.Order.Account.Pubkey, orderRequest.Account)) 
                throw new ForbiddenException();

            return Task.CompletedTask;
        }
    }
}
