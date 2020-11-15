using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    [Subscription(SubscriptionType.AllMarketTickersSubscription)]
    public class AllMarketTickersSubscription : BaseSubscription
    {
        public override bool Equals(object obj)
        {
            return obj is AllMarketTickersSubscription;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GetType().FullName);
        }

        public override void SetValues(string[] values)
        {
            Name = GetNameBuilder().ToString();
        }
    }
}
