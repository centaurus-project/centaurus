using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    [Subscription(SubscriptionType.TradesFeedSubscription)]
    public class TradesFeedSubscription : BaseMarketSubscription
    {
        public override bool Equals(object obj)
        {
            return obj is TradesFeedSubscription subscription &&
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