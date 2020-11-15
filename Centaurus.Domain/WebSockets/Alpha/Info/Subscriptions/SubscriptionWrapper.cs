using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class SubscriptionWrapper
    {
        public SubscriptionType SubscriptionType { get; set; }

        public BaseSubscription Subscription { get; set; }
    }
}
