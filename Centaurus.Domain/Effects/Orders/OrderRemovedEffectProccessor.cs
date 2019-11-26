using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderRemovedEffectProccessor : EffectProcessor<OrderRemovedEffect>
    {
        private Orderbook orderbook;

        public OrderRemovedEffectProccessor(OrderRemovedEffect effect, Orderbook orderbook)
            : base(effect)
        {
            this.orderbook = orderbook;
        }

        public override UpdatedObject[] CommitEffect()
        {
            if (!orderbook.RemoveOrder(Effect.OrderId))
                throw new Exception($"Unable to remove order with id {Effect.OrderId}");
            return new UpdatedObject[] { new UpdatedObject(new Order { OrderId = Effect.OrderId }) };
        }

        public override void RevertEffect()
        {
            var order = new Order { OrderId = Effect.OrderId, Price = Effect.Price, Amount = 0, Pubkey = Effect.Pubkey };
            orderbook.InsertOrder(order);
        }

        public static OrderRemovedEffectProccessor GetProcessor(ulong apex, Orderbook orderbook, Order order)
        {
            return GetProcessor(
                new OrderRemovedEffect { Apex = apex, OrderId = order.OrderId, Price = order.Price, Pubkey = order.Pubkey },
                orderbook
                );
        }

        public static OrderRemovedEffectProccessor GetProcessor(OrderRemovedEffect effect, Orderbook orderbook)
        {
            return new OrderRemovedEffectProccessor(effect, orderbook);
        }
    }
}
