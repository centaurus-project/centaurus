using Centaurus.Exchange;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class MarketDepthUpdate : SubscriptionUpdateBase
    {
        public MarketDepth MarketDepth { get; set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            if (UpdateDate <= lastUpdateDate)
                return null;
            return this;
        }

        public static MarketDepthUpdate Generate(MarketDepth depth, string channelName)
        {
            if (depth == null)
                return null;
            return new MarketDepthUpdate { MarketDepth = depth, UpdateDate = depth.UpdatedAt, ChannelName = channelName };
        }
    }
}
