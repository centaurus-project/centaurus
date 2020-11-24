using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class MarketDepthsManager
    {
        public MarketDepthsManager(List<int> markets, List<double> precisions, IOrderMap orders)
        {
            if (markets == null)
                throw new ArgumentNullException(nameof(markets));
            if (orders == null)
                throw new ArgumentNullException(nameof(orders));
            if (precisions == null)
                throw new ArgumentNullException(nameof(precisions));
            if (precisions.Count < 1)
                precisions = new List<double> { 1 };

            var depths = new Dictionary<int, Dictionary<double, MarketDepth>>();
            foreach (var market in markets)
            {
                depths.Add(market, new Dictionary<double, MarketDepth>());
                foreach (var precision in precisions)
                    depths[market].Add(precision, new MarketDepth(market, precision, orders));
            }
            this.marketDepths = depths.ToImmutableDictionary();
        }

        private ImmutableDictionary<int, Dictionary<double, MarketDepth>> marketDepths;

        public void Restore()
        {
            foreach (var market in marketDepths.Keys)
                foreach (var depthManager in marketDepths[market].Values)
                {
                    depthManager.Restore();
                }
        }

        public void OnOrderUpdates(int market, List<OrderInfo> orderUpdates)
        {
            if (!marketDepths.ContainsKey(market))
                throw new ArgumentException($"Market {market} is not supported.");
            foreach (var depthManager in marketDepths[market].Values)
            {
                depthManager.OnOrderUpdates(orderUpdates);
            }
        }

        public MarketDepth GetDepth(int market, double precision)
        {
            if (!marketDepths.TryGetValue(market, out var currentMarketDepths))
                throw new ArgumentException($"Market {market} is not supported.");
            if (!currentMarketDepths.TryGetValue(precision, out var depht))
                throw new ArgumentException($"Precision {precision} is not supported.");
            return depht;
        }
    }
}
