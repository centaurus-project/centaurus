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
        private Dictionary<string, AnalyticsMarket> Markets = new Dictionary<string, AnalyticsMarket>();

        public AnalyticsOrderMap OrderMap { get; } = new AnalyticsOrderMap();

        public AnalyticsMarket GetMarket(string asset)
        {
            if (Markets.TryGetValue(asset, out AnalyticsMarket market)) return market;
            throw new InvalidOperationException($"Asset {asset} is not supported");
        }

        public AnalyticsMarket AddMarket(string asset)
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

        public AnalyticsOrderbook GetOrderbook(string asset, OrderSide side)
        {
            return GetMarket(asset).GetOrderbook(side);
        }

        public OrderInfoWrapper GetOrder(ulong offerId)
        {
            return OrderMap.GetOrder(offerId);
        }

        public bool RemoveOrder(ulong offerId)
        {
            var order = GetOrder(offerId);
            if (order == null)
                throw new Exception($"Order {offerId} is not found");
            return GetOrderbook(order.Order.Market, order.Order.Side).RemoveOrder(offerId);
        }

        public static AnalyticsExchange RestoreExchange(List<string> assets, List<OrderInfo> orders)
        {
            var exchange = new AnalyticsExchange();
            foreach (var asset in assets)
                exchange.AddMarket(asset);

            foreach (var order in orders)
            {
                var orderbook = exchange.GetOrderbook(order.Market, order.Side);
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
                        GetOrder(order.OrderId).Order.AmountDiff = order.AmountDiff;
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
