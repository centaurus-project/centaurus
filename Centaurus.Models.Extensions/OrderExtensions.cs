using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models.Extensions
{
    public static class OrderExtensions
    {
        public static Order Clone(this Order source)
        {
            return new Order
            {
                Amount = source.Amount,
                OrderId = source.OrderId,
                Price = source.Price,
                Account = source.Account,
                Next = source.Next,
                Prev = source.Prev
            };
        }
    }
}
