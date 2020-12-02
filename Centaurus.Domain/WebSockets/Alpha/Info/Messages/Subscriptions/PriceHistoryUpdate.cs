using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class PriceHistoryUpdate : SubscriptionUpdateBase
    {
        public List<PriceHistoryFrame> Prices { get; set; }

        public static PriceHistoryUpdate Generate(List<PriceHistoryFrame> priceFrames, string channelName)
        {
            if (priceFrames == null || priceFrames.Count < 1)
                return null;
            return new PriceHistoryUpdate { Prices = priceFrames, UpdateDate = priceFrames.Max(f => f.UpdatedAt), ChannelName = channelName };
        }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            if (lastUpdateDate >= UpdateDate || Prices == null || Prices.Count < 1)
                return null;

            if (Prices.All(t => t.UpdatedAt > lastUpdateDate))
                return this;

            var pricesOfInterest = Prices.Where(t => t.UpdatedAt > lastUpdateDate).ToList();
            return new PriceHistoryUpdate { ChannelName = ChannelName, Prices = pricesOfInterest, UpdateDate = UpdateDate };
        }
    }
}