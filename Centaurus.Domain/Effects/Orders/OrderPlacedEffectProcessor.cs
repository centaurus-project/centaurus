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
            //add order to the orderbook
            orderBook.InsertOrder(order);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            orderBook.RemoveOrder(Effect.OrderId);
        }
    }
}
