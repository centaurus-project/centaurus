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

            if (Trades.All(t => t.TradeDate > lastUpdateDate))
                return this;

            var tradesOfInterest = Trades.Where(t => t.TradeDate > lastUpdateDate).ToList();
            return new TradesFeedUpdate { ChannelName = ChannelName, Trades = tradesOfInterest, UpdateDate = UpdateDate };
        }

        public static TradesFeedUpdate Generate(List<Trade> trades, string channelName)
        {
            if (trades == null || trades.Count < 1)
                return null;
            return new TradesFeedUpdate { Trades = trades, UpdateDate = trades.FirstOrDefault().TradeDate, ChannelName = channelName };
        }
    }
}
