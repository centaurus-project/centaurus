using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    [Subscription(SubscriptionType.PriceHistorySubscription)]
    public class PriceHistorySubscription : BaseMarketSubscription
    {
        public OHLCFramePeriod FramePeriod { get; set; }

        public override bool Equals(object obj)
        {
            return obj is PriceHistorySubscription subscription &&
                   Market == subscription.Market &&
                   FramePeriod == subscription.FramePeriod;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Market, FramePeriod);
        }

        public override void SetValues(string[] values)
        {
            base.SetValues(values);
            Name = GetNameBuilder().ToString();
        }

        protected override StringBuilder GetNameBuilder()
        {
            return base.GetNameBuilder()
                .Append("_").Append(FramePeriod);
        }
    }
}