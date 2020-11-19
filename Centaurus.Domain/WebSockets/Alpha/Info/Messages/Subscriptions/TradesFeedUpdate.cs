using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TradesFeedUpdate : SubscriptionUpdateBase
    {
        public List<Trade> Trades { get; set; }
    }
}
