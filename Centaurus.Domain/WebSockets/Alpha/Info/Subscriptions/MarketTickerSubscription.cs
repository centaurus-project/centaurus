using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{

    [Subscription(SubscriptionType.MarketTickerSubscription)]
    public class MarketTickerSubscription : BaseMarketSubscription
    {
        public override bool Equals(object obj)
        {
            return obj is MarketTickerSubscription subscription &&
                   Market == subscription.Market;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Market);
        }

        public override void SetValues(string[] values)
        {
            base.SetValues(values);
            Name = GetNameBuilder().ToString();
        }
    }
}
