using Centaurus.Models;
using Centaurus.Models.Analytics;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class AnalyticsExchange
    {
        private Dictionary<int, AnalyticsMarket> Markets = new Dictionary<int, AnalyticsMarket>();

        public AnalyticsOrderMap OrderMap { get; } = new AnalyticsOrderMap();

        public AnalyticsMarket GetMarket(int asset)
        {
            if (Markets.TryGetValue(asset, out AnalyticsMarket market)) return market;
            throw new InvalidOperationException($"Asset {asset} is not supported");
        }

        public AnalyticsMarket AddMarket(int asset)
        {
            var market = new AnalyticsMarket(asset, OrderMap);
            Markets.Add(asset, market);
            return market;
        }

        public void Clear()
        {
            Markets.Clear();
            OrderMap.Clear();
        }

        public AnalyticsOrderbook GetOrderbook(ulong offerId)
        {
            var parts = OrderIdConverter.Decode(offerId);
            return GetOrderbook(parts.Asset, parts.Side);
        }

        public AnalyticsOrderbook GetOrderbook(int asset, OrderSide side)
        {
            return GetMarket(asset).GetOrderbook(side);
        }

        public OrderInfoWrapper GetOrder(ulong offerId)
        {
            return GetOrderbook(offerId).GetOrder(offerId);
        }

        public bool RemoveOrder(ulong offerId)
        {
            return GetOrderbook(offerId).RemoveOrder(offerId);
        }

        public static AnalyticsExchange RestoreExchange(List<int> assets, List<OrderInfo> orders)
        {
            var exchange = new AnalyticsExchange();
            foreach (var asset in assets)
                exchange.AddMarket(asset);

            foreach (var order in orders)
            {
                var orderData = OrderIdConverter.Decode(order.OrderId);
                var market = exchange.GetMarket(orderData.Asset);
                var orderbook = market.GetOrderbook(orderData.Side);
                orderbook.InsertOrder(new OrderInfoWrapper { Order = order });
            }
            return exchange;
        }

        public void OnUpdates(ExchangeUpdate updates)
        {
            var market = GetMarket(updates.Market);
            foreach (var order in updates.OrderUpdates)
            {
                switch (order.State)
                {
                    case OrderState.New:
                        market.GetOrderbook(order.Side).InsertOrder(new OrderInfoWrapper { Order = order });
                        break;
                    case OrderState.Updated:
                        GetOrder(order.OrderId).Order.Amount = order.Amount;
                        break;
                    case OrderState.Deleted:
                        RemoveOrder(order.OrderId);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
