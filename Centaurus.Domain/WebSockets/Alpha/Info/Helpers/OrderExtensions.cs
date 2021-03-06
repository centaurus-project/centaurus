using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class OrderExtensions
    {
        public static OrderInfo ToOrderInfo(this Order order, OrderState state = OrderState.New)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));
            var decodedId = OrderIdConverter.Decode(order.OrderId);
            return new OrderInfo
            {
                OrderId = order.OrderId,
                AmountDiff = order.Amount,
                QuoteAmountDiff = order.QuoteAmount,
                Market = decodedId.Asset,
                Price = order.Price,
                Side = decodedId.Side,
                State = state
            };
        }
    }
}
