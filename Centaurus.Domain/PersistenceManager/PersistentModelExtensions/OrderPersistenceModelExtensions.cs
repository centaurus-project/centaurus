using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;

namespace Centaurus.Domain
{
    public static class OrderPersistenceModelExtensions
    {
        public static Order ToDomainModel(this OrderPersistentModel order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            return new Order
            {
                Amount = order.Amount,
                QuoteAmount = order.QuoteAmount,
                OrderId = order.Apex,
                Price = order.Price,
                Asset = order.Asset,
                Side = (OrderSide)order.Side
            };
        }

        public static OrderPersistentModel ToPersistentModel(this Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            return new OrderPersistentModel
            {
                Amount = order.Amount,
                QuoteAmount = order.QuoteAmount,
                Apex = order.OrderId,
                Price = order.Price,
                Asset = order.Asset,
                Side = (int)order.Side
            };
        }
    }
}
