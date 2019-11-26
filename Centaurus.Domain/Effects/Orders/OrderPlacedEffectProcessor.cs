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
            this.orderBook = orderBook;
            this.order = order;
        }

        public override UpdatedObject[] CommitEffect()
        {
            //add order to the orderbook
            orderBook.InsertOrder(order);
            return new UpdatedObject[] { new UpdatedObject(order) };
        }

        public override void RevertEffect()
        {
            orderBook.RemoveOrder(Effect.OrderId);
        }

        public static OrderPlacedEffectProcessor GetProcessor(ulong apex, Orderbook orderBook, Order order, int asset, OrderSides side)
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

            return GetProcessor(effect, orderBook, order);
        }

        public static OrderPlacedEffectProcessor GetProcessor(OrderPlacedEffect effect, Orderbook orderBook, Order order)
        {
            return new OrderPlacedEffectProcessor(effect, orderBook, order);
        }
    }
}
