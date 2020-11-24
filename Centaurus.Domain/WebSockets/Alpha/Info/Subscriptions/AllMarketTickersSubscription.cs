using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class AllMarketTickersSubscription : BaseSubscription
    {
        public override string Name => "AllMarketTickersSubscription";

        public override bool Equals(object obj)
        {
            return obj is AllMarketTickersSubscription;
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
