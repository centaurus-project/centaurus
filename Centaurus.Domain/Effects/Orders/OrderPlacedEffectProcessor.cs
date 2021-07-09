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
        private string baseAsset;

        public OrderPlacedEffectProcessor(OrderPlacedEffect effect, AccountWrapper account, OrderbookBase orderBook, OrderWrapper order, string baseAsset)
            :base(effect, account)
        {
            this.orderBook = orderBook ?? throw new ArgumentNullException(nameof(orderBook));
            this.order = order ?? throw new ArgumentNullException(nameof(order));
            this.baseAsset = baseAsset ?? throw new ArgumentNullException(nameof(baseAsset));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();

            //lock order reserve
            if (order.Order.Side == OrderSide.Buy)
                AccountWrapper.Account.GetBalance(baseAsset).UpdateLiabilities(order.Order.QuoteAmount, UpdateSign.Plus);
            else
                AccountWrapper.Account.GetBalance(order.Order.Asset).UpdateLiabilities(order.Order.Amount, UpdateSign.Plus);

            //add order to the orderbook
            orderBook.InsertOrder(order);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();

            orderBook.RemoveOrder(Effect.Apex, out _);

            if (order.Order.Side == OrderSide.Buy)
                AccountWrapper.Account.GetBalance(baseAsset).UpdateLiabilities(order.Order.QuoteAmount, UpdateSign.Minus);
            else
                AccountWrapper.Account.GetBalance(order.Order.Asset).UpdateLiabilities(order.Order.Amount, UpdateSign.Minus);
        }
    }
}
