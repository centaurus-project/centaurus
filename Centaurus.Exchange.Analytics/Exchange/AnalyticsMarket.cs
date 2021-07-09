using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class AnalyticsMarket
    {
        public AnalyticsMarket(string asset, AnalyticsOrderMap orderMap)
        {
            Asset = asset;
            Asks = new AnalyticsOrderbook(orderMap, OrderSide.Sell);
            Bids = new AnalyticsOrderbook(orderMap, OrderSide.Buy);
        }

        public string Asset { get; }

        public AnalyticsOrderbook Asks { get; }

        public AnalyticsOrderbook Bids { get; }

        public AnalyticsOrderbook GetOrderbook(OrderSide side)
        {
            return side == OrderSide.Buy ? Bids : Asks;
        }
    }
}
