using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderRemovedEffectProccessor : EffectProcessor<OrderRemovedEffect>
    {
        private Orderbook orderbook;
        private AccountStorage accountStorage;

        public OrderRemovedEffectProccessor(OrderRemovedEffect effect, Orderbook orderbook, AccountStorage accountStorage)
            : base(effect)
        {
            this.orderbook = orderbook;
            this.accountStorage = accountStorage;
        }

        public override void CommitEffect()
        {
            if (!orderbook.RemoveOrder(Effect.OrderId))
                throw new Exception($"Unable to remove order with id {Effect.OrderId}");
        }

        public override void RevertEffect()
        {
            var order = new Order { OrderId = Effect.OrderId, Price = Effect.Price, Amount = 0, Account = accountStorage.GetAccount(Effect.Pubkey).Account };
            orderbook.InsertOrder(order);
        }
    }
}
