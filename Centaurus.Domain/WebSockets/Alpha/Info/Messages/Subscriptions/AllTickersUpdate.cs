using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class AllTickersUpdate : SubscriptionUpdateBase
    {
        public List<MarketTicker> Tickers { get; set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            if (lastUpdateDate >= UpdateDate || Tickers == null || Tickers.Count < 1)
                return null;

            if (Tickers.All(t => t.UpdatedAt > lastUpdateDate))
                return this;

            var tickersOfInterest = Tickers.Where(t => t.UpdatedAt > lastUpdateDate).ToList();
            return new AllTickersUpdate { ChannelName = ChannelName, Tickers = tickersOfInterest, UpdateDate = UpdateDate };
        }

        public static AllTickersUpdate Generate(List<MarketTicker> tickers, string channelName)
        {
            if (tickers == null && tickers.Count < 1)
                return null;
            return new AllTickersUpdate { Tickers = tickers, UpdateDate = tickers.Max(t => t.UpdatedAt), ChannelName = channelName };
        }
    }
}
