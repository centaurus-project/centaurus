using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class MarketTickerUpdate: SubscriptionUpdateBase
    {
        public MarketTicker MarketTicker { get; set; }
    }
}
