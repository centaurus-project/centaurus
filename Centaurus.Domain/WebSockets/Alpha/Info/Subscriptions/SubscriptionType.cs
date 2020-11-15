using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public enum SubscriptionType
    {
        MarketTickerSubscription = 0,
        AllMarketTickersSubscription = 1,
        TradesFeedSubscription = 2,
        DepthsSubscription = 3,
        PriceHistorySubscription = 4
    }
}
