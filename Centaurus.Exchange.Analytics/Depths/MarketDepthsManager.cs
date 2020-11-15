using Centaurus.Analytics;
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

            var depths = new Dictionary<int, Dictionary<double, SingleMarketDepths>>();
            foreach (var market in markets)
            {
                depths.Add(market, new Dictionary<double, SingleMarketDepths>());
                foreach (var precision in precisions)
                    depths[market].Add(precision, new SingleMarketDepths(market, precision, orders));
            }
            this.depths = depths.ToImmutableDictionary();
        }

        private ImmutableDictionary<int, Dictionary<double, SingleMarketDepths>> depths;

        public void Restore()
        {
            foreach (var market in depths.Keys)
                foreach (var depthManager in depths[market].Values)
                {
                    depthManager.Restore();
                }
        }

        public void OnOrderUpdates(int market, List<OrderUpdate> orderUpdates)
        {
            if (!depths.ContainsKey(market))
                throw new ArgumentException($"Market {market} is not supported.");
            foreach (var depthManager in depths[market].Values)
            {
                depthManager.OnOrderUpdates(orderUpdates);
            }
        }
    }
}
