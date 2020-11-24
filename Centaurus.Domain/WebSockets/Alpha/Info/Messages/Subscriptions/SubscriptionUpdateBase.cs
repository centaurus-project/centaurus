using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class SubscriptionUpdateBase
    {
        public string ChannelName { get; set; }

        public DateTime UpdateDate { get; set; }

        public abstract SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate);
    }
}
