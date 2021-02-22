using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderPlacedEffectProcessor : EffectProcessor<OrderPlacedEffect>
    {
        private Orderbook orderBook;
        private Order order;

        public OrderPlacedEffectProcessor(OrderPlacedEffect effect, Orderbook orderBook, Order order)
            :base(effect)
        {
            this.orderBook = orderBook ?? throw new ArgumentNullException(nameof(orderBook));
            this.order = order ?? throw new ArgumentNullException(nameof(order));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();

            //lock order reserve
            var decodedId = OrderIdConverter.Decode(order.OrderId);
            if (decodedId.Side == OrderSide.Buy)
                order.AccountWrapper.Account.GetBalance(0).UpdateLiabilities(order.QuoteAmount);
            else
                order.AccountWrapper.Account.GetBalance(decodedId.Asset).UpdateLiabilities(order.Amount);

            //add order to the orderbook
            orderBook.InsertOrder(order);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();

            orderBook.RemoveOrder(Effect.OrderId);

            var decodedId = OrderIdConverter.Decode(order.OrderId);
            if (decodedId.Side == OrderSide.Buy)
                order.AccountWrapper.Account.GetBalance(0).UpdateLiabilities(-order.QuoteAmount);
            else
                order.AccountWrapper.Account.GetBalance(decodedId.Asset).UpdateLiabilities(-order.Amount);
        }
    }
}
