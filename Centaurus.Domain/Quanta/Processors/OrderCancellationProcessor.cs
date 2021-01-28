﻿using Centaurus.Domain.Quanta.Contexts;
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

            //lock order reserve
            if (context.OrderSide == OrderSide.Buy)
                //TODO: check this - potential rounding error with multiple trades
                context.EffectProcessors.AddUpdateLiabilities(orderRequest.AccountWrapper.Account, 0, -context.XmlAmount);
            else
                context.EffectProcessors.AddUpdateLiabilities(orderRequest.AccountWrapper.Account, context.Asset, -context.Order.Amount);

            context.EffectProcessors.AddOrderRemoved(context.Orderbook, context.Order);

            var resultMessage = context.Envelope.CreateResult(ResultStatusCodes.Success, context.EffectProcessors.Effects);
            return Task.FromResult(resultMessage);
        }


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
                throw new BadRequestException($"Order {orderRequest.OrderId} is not found.{(quantum.Apex != default ? $" Apex {quantum.Apex}" : "")}");

            //TODO: check that lot size is greater than minimum allowed lot
            if (context.Order.Account.Id != orderRequest.Account)
                throw new ForbiddenException();

            if (context.OrderSide == OrderSide.Buy)
            {
                context.XmlAmount = OrderMatcher.EstimateTradedXlmAmount(context.Order.Amount, context.Order.Price);
                var balance = orderRequest.AccountWrapper.Account.GetBalance(0);
                if (balance.Liabilities < context.XmlAmount)
                    throw new BadRequestException("Xml liabilities is less than order size.");
            }
            else
            {
                var balance = orderRequest.AccountWrapper.Account.GetBalance(orderData.Asset);
                if (balance == null)
                    throw new BadRequestException("Balance for asset is not found.");
                if (balance.Liabilities < context.Order.Amount)
                    throw new BadRequestException("Asset liabilities is less than order size.");
            }

            return Task.CompletedTask;
        }
    }
}
