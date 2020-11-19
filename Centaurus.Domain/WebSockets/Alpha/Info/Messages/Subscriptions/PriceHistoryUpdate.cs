using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PriceHistoryUpdate : SubscriptionUpdateBase
    {
        public List<OHLCFrame> Prices { get; set; }
    }
}