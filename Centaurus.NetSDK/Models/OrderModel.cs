using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Centaurus.NetSDK
{
    public class OrderModel
    {
        public ulong OrderId { get; set; }

        public double Price { get; set; }

        public string PriceStr => Price.ToString("0.#########", CultureInfo.InvariantCulture);

        public ulong Amount { get; set; }

        //public string AmountXdr => stellar_dotnet_sdk.Amount.FromXdr(Amount);

        public string Asset { get; set; }

        public OrderSide Side { get; set; }

        public static OrderModel FromOrder(Order order)
        {
            return new OrderModel
            {
                OrderId = order.OrderId,
                Price = order.Price,
                Amount = order.Amount,
                Asset = order.Asset
            };
        }
    }
}
