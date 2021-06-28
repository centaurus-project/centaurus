using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderPlacedEffectProcessor : ClientEffectProcessor<OrderPlacedEffect>
    {
        private OrderbookBase orderBook;
        private OrderWrapper order;

        public OrderPlacedEffectProcessor(OrderPlacedEffect effect, AccountWrapper account, OrderbookBase orderBook, OrderWrapper order)
            :base(effect, account)
        {
            this.orderBook = orderBook ?? throw new ArgumentNullException(nameof(orderBook));
            this.order = order ?? throw new ArgumentNullException(nameof(order));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();

            //lock order reserve
            var decodedId = OrderIdConverter.Decode(order.Order.OrderId);
            if (decodedId.Side == OrderSide.Buy)
                AccountWrapper.Account.GetBalance(0).UpdateLiabilities(order.Order.QuoteAmount);
            else
                AccountWrapper.Account.GetBalance(decodedId.Asset).UpdateLiabilities(order.Order.Amount);

            //add order to the orderbook
            orderBook.InsertOrder(order);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();

            orderBook.RemoveOrder(Effect.OrderId, out _);

            var decodedId = OrderIdConverter.Decode(order.OrderId);
            if (decodedId.Side == OrderSide.Buy)
                AccountWrapper.Account.GetBalance(0).UpdateLiabilities(-order.Order.QuoteAmount);
            else
                AccountWrapper.Account.GetBalance(decodedId.Asset).UpdateLiabilities(-order.Order.Amount);
        }
    }
}
