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

        public override void CommitEffect()
        {
            if (!orderbook.RemoveOrder(Effect.OrderId))
                throw new Exception($"Unable to remove order with id {Effect.OrderId}");
        }

        public override void RevertEffect()
        {
            var order = new Order { OrderId = Effect.OrderId, Price = Effect.Price, Amount = 0, Pubkey = Effect.Pubkey };
            orderbook.InsertOrder(order);
        }

        public static OrderRemovedEffectProccessor GetProccessor(ulong apex, Orderbook orderbook, Order order)
        {
            return new OrderRemovedEffectProccessor(
                new OrderRemovedEffect { Apex = apex, OrderId = order.OrderId, Price = order.Price, Pubkey = order.Pubkey },
                orderbook
                );
        }
    }
}
