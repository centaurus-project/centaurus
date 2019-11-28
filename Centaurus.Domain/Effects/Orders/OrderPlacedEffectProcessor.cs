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

        public override void CommitEffect()
        {
            //add order to the orderbook
            orderBook.InsertOrder(order);
        }

        public override void RevertEffect()
        {
            orderBook.RemoveOrder(Effect.OrderId);
        }
    }
}
