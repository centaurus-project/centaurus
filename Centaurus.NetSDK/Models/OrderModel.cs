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
                    AssetId = decoded.Asset;
                    Side = decoded.Side;
                }
            }
        }

        public double Price { get; set; }

        public string PriceStr => Price.ToString("0.#########", CultureInfo.InvariantCulture);

        public long Amount { get; set; }

        public string AmountXdr => stellar_dotnet_sdk.Amount.FromXdr(Amount);

        public int AssetId { get; set; }

        public string Asset { get; set; }

        public OrderSide Side { get; set; }

        public static OrderModel FromOrder(Order order, ConstellationInfo constellation)
        {
            var orderModel = new OrderModel
            {
                OrderId = order.OrderId,
                Price = order.Price,
                Amount = order.Amount
            };

            orderModel.Asset = constellation.Assets.FirstOrDefault(a => a.Id == orderModel.AssetId)?.DisplayName ?? orderModel.AssetId.ToString();
            return orderModel;
        }
    }
}
