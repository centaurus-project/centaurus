using Centaurus.DAL.Models;
using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class OrderModelExtensions
    {
        public static OrderWrapper ToOrder(this OrderModel order, AccountWrapper account)
        {
            return new OrderWrapper(
                new Order
                {
                    Amount = order.Amount,
                    QuoteAmount = order.QuoteAmount,
                    OrderId = unchecked((ulong)order.Id),
                    Price = order.Price
                },
                account
            );
        }
    }
}
