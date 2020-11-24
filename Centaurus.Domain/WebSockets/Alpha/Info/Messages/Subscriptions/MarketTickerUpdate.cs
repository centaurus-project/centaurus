using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class MarketTickerUpdate: SubscriptionUpdateBase
    {
        public MarketTicker MarketTicker { get; set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            if (UpdateDate <= lastUpdateDate)
                return null;
            return this;
        }

        public static MarketTickerUpdate Generate(MarketTicker ticker, string channelName)
        {
            if (ticker == null)
                return null;
            return new MarketTickerUpdate { MarketTicker = ticker, UpdateDate = ticker.UpdatedAt, ChannelName = channelName };
        }
    }
}
