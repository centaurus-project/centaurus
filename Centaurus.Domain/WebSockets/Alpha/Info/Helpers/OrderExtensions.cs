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
            return new OrderInfo
            {
                OrderId = order.Apex,
                AmountDiff = order.Amount,
                QuoteAmountDiff = order.QuoteAmount,
                Market = order.Asset,
                Price = order.Price,
                Side = order.Side,
                State = state
            };
        }
    }
}
