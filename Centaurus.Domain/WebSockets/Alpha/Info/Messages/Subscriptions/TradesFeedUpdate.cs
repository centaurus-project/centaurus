using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class TradesFeedUpdate : SubscriptionUpdateBase
    {
        public List<Trade> Trades { get; set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            if (lastUpdateDate >= UpdateDate || Trades == null || Trades.Count < 1)
                return null;

            if (Trades.All(t => t.Timestamp > lastUpdateDate.Ticks))
                return this;

            var tradesOfInterest = Trades.Where(t => t.Timestamp > lastUpdateDate.Ticks).ToList();
            return new TradesFeedUpdate { ChannelName = ChannelName, Trades = tradesOfInterest, UpdateDate = UpdateDate };
        }

        public static TradesFeedUpdate Generate(List<Trade> trades, string channelName)
        {
            if (trades == null || trades.Count < 1)
                return null;
            return new TradesFeedUpdate { Trades = trades, UpdateDate = new DateTime(trades.FirstOrDefault().Timestamp, DateTimeKind.Utc), ChannelName = channelName };
        }
    }
}
