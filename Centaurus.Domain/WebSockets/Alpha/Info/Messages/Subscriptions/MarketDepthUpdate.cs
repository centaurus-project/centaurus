using Centaurus.Exchange;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class MarketDepthUpdate : SubscriptionUpdateBase
    {
        public MarketDepth MarketDepth { get; set; }
    }
}
