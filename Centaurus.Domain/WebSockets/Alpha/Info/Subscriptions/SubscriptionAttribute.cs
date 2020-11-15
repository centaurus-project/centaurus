using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SubscriptionAttribute : Attribute
    {
        public SubscriptionAttribute(SubscriptionType subscriptionType)
        {
            SubscriptionType = subscriptionType;
        }

        public SubscriptionType SubscriptionType { get; }
    }
}
