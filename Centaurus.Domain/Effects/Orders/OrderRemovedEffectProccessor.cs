using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderRemovedEffectProccessor : ClientEffectProcessor<OrderRemovedEffect>
    {
        private OrderbookBase orderbook;
        private string baseAsset;

        public OrderRemovedEffectProccessor(OrderRemovedEffect effect, AccountWrapper account, OrderbookBase orderbook, string baseAsset)
            : base(effect, account)
        {
            this.orderbook = orderbook ?? throw new ArgumentNullException(nameof(orderbook));
            this.baseAsset = baseAsset ?? throw new ArgumentNullException(nameof(baseAsset));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            if (!orderbook.RemoveOrder(Effect.OrderId, out _))
                throw new Exception($"Unable to remove order with id {Effect.Apex}");

            if (Effect.Side == OrderSide.Buy)
                AccountWrapper.Account.GetBalance(baseAsset).UpdateLiabilities(Effect.QuoteAmount, UpdateSign.Minus);
            else
                AccountWrapper.Account.GetBalance(Effect.Asset).UpdateLiabilities(Effect.Amount, UpdateSign.Minus);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();

            if (Effect.Side == OrderSide.Buy)
                AccountWrapper.Account.GetBalance(baseAsset).UpdateLiabilities(Effect.QuoteAmount, UpdateSign.Plus);
            else
                AccountWrapper.Account.GetBalance(Effect.Asset).UpdateLiabilities(Effect.Amount, UpdateSign.Plus);

            var order = new OrderWrapper(
                new Order
                {
                    OrderId = Effect.OrderId,
                    Amount = Effect.Amount,
                    QuoteAmount = Effect.QuoteAmount,
                    Price = Effect.Price,
                    Asset = Effect.Asset,
                    Side = Effect.Side
                },
                AccountWrapper
            );
            orderbook.InsertOrder(order);
        }
    }
}
