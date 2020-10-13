using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.SDK.Models
{
    public class OrderModel
    {
        private ulong orderId;
        public ulong OrderId 
        {
            get => orderId;
            set
            {
                orderId = value;
                if (orderId != default)
                {
                    var decoded = OrderIdConverter.Decode(orderId);
                    Asset = decoded.Asset;
                    Side = decoded.Side;
                }
            }
        }

        public double Price { get; set; }

        public string PriceStr => Price.ToString("0.#########");

        public long Amount { get; set; }

        public string AmountXdr => stellar_dotnet_sdk.Amount.FromXdr(Amount);

        public int Asset { get; set; }

        public OrderSides Side { get; set; }

        public static OrderModel FromOrder(Order order)
        {
            return new OrderModel
            {
                OrderId = order.OrderId,
                Price = order.Price,
                Amount = order.Amount
            };
        }
    }
}
