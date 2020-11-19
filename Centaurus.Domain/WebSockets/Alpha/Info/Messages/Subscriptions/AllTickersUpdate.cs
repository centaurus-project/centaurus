using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class AllTickersUpdate: SubscriptionUpdateBase
    {
        public List<MarketTicker> Tickers { get; set; }
    }
}
