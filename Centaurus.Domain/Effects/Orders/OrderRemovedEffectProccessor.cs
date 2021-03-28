using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderRemovedEffectProccessor : EffectProcessor<OrderRemovedEffect>
    {
        private OrderbookBase orderbook;

        public OrderRemovedEffectProccessor(OrderRemovedEffect effect, OrderbookBase orderbook)
            : base(effect)
        {
            this.orderbook = orderbook ?? throw new ArgumentNullException(nameof(orderbook));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            if (!orderbook.RemoveOrder(Effect.OrderId, out _))
                throw new Exception($"Unable to remove order with id {Effect.OrderId}");

            var decodedId = OrderIdConverter.Decode(Effect.OrderId);
            if (decodedId.Side == OrderSide.Buy)
                Effect.AccountWrapper.Account.GetBalance(0).UpdateLiabilities(-Effect.QuoteAmount);
            else
                Effect.AccountWrapper.Account.GetBalance(decodedId.Asset).UpdateLiabilities(-Effect.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();

            var decodedId = OrderIdConverter.Decode(Effect.OrderId);
            if (decodedId.Side == OrderSide.Buy)
                Effect.AccountWrapper.Account.GetBalance(0).UpdateLiabilities(Effect.QuoteAmount);
            else
                Effect.AccountWrapper.Account.GetBalance(decodedId.Asset).UpdateLiabilities(Effect.Amount);

            var order = new Order
            {
                OrderId = Effect.OrderId,
                Amount = Effect.Amount,
                QuoteAmount = Effect.QuoteAmount,
                Price = Effect.Price,
                AccountWrapper = Effect.AccountWrapper
            };
            orderbook.InsertOrder(order);
        }
    }
}
