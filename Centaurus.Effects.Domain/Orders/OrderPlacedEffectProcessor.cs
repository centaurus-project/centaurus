using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderPlacedProcessor : EffectProcessor<OrderPlacedEffect>
    {
        private Orderbook orderBook;
        private Order order;

        public OrderPlacedProcessor(OrderPlacedEffect effect, Orderbook orderBook, Order order)
            :base(effect)
        {
            this.orderBook = orderBook;
            this.order = order;
        }

        public override void CommitEffect()
        {
            //add order to the orderbook
            orderBook.InsertOrder(order);
        }

        public override void RevertEffect()
        {
            orderBook.RemoveOrder(Effect.OrderId);
        }

        public static OrderPlacedProcessor GetProcessor(ulong apex, Orderbook orderBook, Order order, int asset, OrderSides side)
        {
            var effect = new OrderPlacedEffect
            {
                Apex = apex,
                Pubkey = order.Pubkey,
                Asset = asset,
                Amount = order.Amount,
                Price = order.Price,
                OrderId = order.OrderId,
                OrderSide = side
            };

            return new OrderPlacedProcessor(effect, orderBook, order);
        }
    }
}
