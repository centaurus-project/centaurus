using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TradesFeedSubscription : BaseMarketSubscription
    {
        public override string Name => $"TradesFeedSubscription@{Market}";

        public override bool Equals(object obj)
        {
            return obj is TradesFeedSubscription subscription &&
                   Market == subscription.Market;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Market);
        }

        public override void SetValues(string values)
        {
            SetMarket(values);
        }
    }
}