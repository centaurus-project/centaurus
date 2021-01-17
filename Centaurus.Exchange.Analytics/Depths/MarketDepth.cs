using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange
{
    public class MarketDepthPrice
    {
        public MarketDepthPrice(double price)
        {
            Price = price;
        }

        public long Amount { get; set; }

        public double Price { get; }

        public List<ulong> Orders { get; } = new List<ulong>();
    }

    public class MarketDepth
    {

        public MarketDepth(int market, double precision, AnalyticsOrderMap orderMap, int maxLevelCount = 20)
        {
            if (orderMap == null)
                throw new ArgumentNullException(nameof(orderMap));
            Market = market;
            Precision = precision;

            var splitted = precision.ToString(CultureInfo.InvariantCulture).Split('.');
            decimalsCount = splitted.Length > 1 ? splitted[1].Length : 0;
            this.maxPricesCount = maxLevelCount;
            this.orderMap = orderMap;
        }

        private Dictionary<OrderSide, List<MarketDepthPrice>> prices = new Dictionary<OrderSide, List<MarketDepthPrice>>
        {
            { OrderSide.Buy, new List<MarketDepthPrice>() },
            { OrderSide.Sell, new List<MarketDepthPrice>() }
        };

        private int decimalsCount;
        private int maxPricesCount;
        private AnalyticsOrderMap orderMap;

        public void OnOrderUpdates(List<OrderInfo> orders, DateTime updateDate)
        {
            if (orders == null)
                throw new ArgumentNullException(nameof(orders));
            var updatedSides = new List<OrderSide>();
            foreach (var order in orders)
            {
                var isUpdated = false;
                switch (order.State)
                {
                    case OrderState.New:
                        isUpdated = AddOrder(order);
                        break;
                    case OrderState.Updated:
                        isUpdated = UpdateOrder(order);
                        break;
                    case OrderState.Deleted:
                        isUpdated = RemoveOrder(order);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported order state {order.State}.");
                }
                if (!updatedSides.Contains(order.Side))
                    updatedSides.Add(order.Side);
            }

            if (updatedSides.Count > 0)
            {
                var sourcesToUpdate = updatedSides.Select(s => prices.First(p => p.Key == s)).ToList();
                var lastOrderId = default(ulong);
                foreach (var source in sourcesToUpdate)
                {
                    var prices = source.Value;
                    var isTrimmed = false;
                    while (prices.Count > maxPricesCount)
                    {
                        prices.RemoveAt(prices.Count - 1);
                        isTrimmed = true;
                    }
                    if (isTrimmed)
                        updatedSides.Remove(source.Key);
                    lastOrderId = Math.Max(prices.LastOrDefault()?.Orders.LastOrDefault() ?? 0, lastOrderId);
                }
                if (updatedSides.Count > 0)
                    Fill(lastOrderId, updatedSides);
            }

            UpdatedAt = updateDate;
        }

        public List<MarketDepthPrice> Asks => prices[OrderSide.Buy];

        public List<MarketDepthPrice> Bids => prices[OrderSide.Sell];

        public int Market { get; }

        public double Precision { get; }

        public DateTime UpdatedAt { get; private set; }

        public void Restore(DateTime updateDate)
        {
            Fill(0, new List<OrderSide> { OrderSide.Buy, OrderSide.Sell });

            UpdatedAt = updateDate;
        }

        private void Fill(ulong orderId, List<OrderSide> sides)
        {
            while (true)
            {
                var order = orderMap.GetNextOrder(orderId);
                if (order == null)
                    break;

                orderId = order.OrderId;
                if (!sides.Contains(order.Side))
                    continue;

                if (!AddOrder(order))
                    break;
            }
        }

        private double NormalizePrice(double price)
        {
            return decimalsCount > 0
                ? Math.Round(price, decimalsCount) //TODO: which way should we round
                : ((int)(price / Precision)) * Precision;
        }

        private bool AddOrder(OrderInfo order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            var price = NormalizePrice(order.Price);
            var source = prices[order.Side];
            var currentPrice = source.FirstOrDefault(p => p.Price == price);
            if (currentPrice == null)
            {
                currentPrice = new MarketDepthPrice(price);
                var indexToInsert = source.FindIndex(p => p.Price < price);
                if (indexToInsert == -1 && source.Count == maxPricesCount)
                    return false;

                if (indexToInsert == -1)
                    source.Add(currentPrice);
                else
                    source.Insert(indexToInsert, currentPrice);
            }
            currentPrice.Amount += order.Amount;
            currentPrice.Orders.Add(order.OrderId);

            return true;
        }

        private bool RemoveOrder(OrderInfo order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            var price = NormalizePrice(order.Price);
            var source = prices[order.Side];
            var currentPrice = source.FirstOrDefault(p => p.Price == price);
            if (currentPrice == null)
                return false;
            currentPrice.Amount += -(order.Amount);
            currentPrice.Orders.Remove(order.OrderId);
            if (currentPrice.Amount == 0)
                source.Remove(currentPrice);
            return true;
        }

        private bool UpdateOrder(OrderInfo order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            var price = NormalizePrice(order.Price);
            var source = prices[order.Side];
            var currentPrice = source.FirstOrDefault(p => p.Price == price);
            if (currentPrice == null)
                return false;
            currentPrice.Amount += -(order.Amount);
            return true;
        }
    }
}