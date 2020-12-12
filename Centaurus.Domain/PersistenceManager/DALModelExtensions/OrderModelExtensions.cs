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
                OrderId = unchecked((ulong)order.OrderId),
                Price = order.Price,
                Account = accountStorage.GetAccount(order.Pubkey).Account
            };
        }
    }
}
