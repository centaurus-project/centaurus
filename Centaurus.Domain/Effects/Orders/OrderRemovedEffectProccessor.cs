using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderRemovedEffectProccessor : EffectProcessor<OrderRemovedEffect>
    {
        private Orderbook orderbook;
        private Account account;

        public OrderRemovedEffectProccessor(OrderRemovedEffect effect, Orderbook orderbook, Account account)
            : base(effect)
        {
            this.orderbook = orderbook ?? throw new ArgumentNullException(nameof(orderbook));
            this.account = account ?? throw new ArgumentNullException(nameof(account));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            if (!orderbook.RemoveOrder(Effect.OrderId))
                throw new Exception($"Unable to remove order with id {Effect.OrderId}");
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            var order = new Order
            {
                OrderId = Effect.OrderId,
                Amount = Effect.Amount,
                QuoteAmount = Effect.QuoteAmount,
                Price = Effect.Price,
                Account = account
            };
            orderbook.InsertOrder(order);
        }
    }
}
