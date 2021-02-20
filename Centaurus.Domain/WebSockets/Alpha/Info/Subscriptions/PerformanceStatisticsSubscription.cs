using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PerformanceStatisticsSubscription : BaseSubscription
    {
        public const string SubscriptionName = "PerformanceStatisticsSubscription";

        public override string Name => SubscriptionName;

        public override bool Equals(object obj)
        {
            return obj is PerformanceStatisticsSubscription;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GetType().FullName);
        }

        public override void SetValues(string values)
        {
            return;
        }
    }
}
