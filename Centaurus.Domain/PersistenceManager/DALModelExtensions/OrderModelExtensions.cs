using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class OrderModelExtensions
    {
        public static Order ToOrder(this OrderModel order, AccountStorage accountStorage)
        {
            return new Order
            {
                Amount = order.Amount,
                QuoteAmount = order.QuoteAmount,
                OrderId = unchecked((ulong)order.Id),
                Price = order.Price,
                AccountWrapper = accountStorage.GetAccount(order.Account)
            };
        }
    }
}
