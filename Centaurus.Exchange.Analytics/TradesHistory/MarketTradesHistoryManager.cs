using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class MarketTradesHistoryManager
    {
        public MarketTradesHistoryManager(int market, int maxSize = 100)
        {
            Market = market;
            this.maxSize = maxSize;
        }

        public int Market { get; }

        public DateTime LastUpdated { get; private set; }

        private int maxSize;
        private LinkedList<Trade> trades = new LinkedList<Trade>();

        public void OnTrade(List<Trade> newTrades)
        {
            foreach (var trade in newTrades)
                trades.AddFirst(trade);
            if (trades.Count > maxSize)
                trades.RemoveLast();
            LastUpdated = DateTime.UtcNow;
        }

        public List<Trade> GetLastTrades(int limit = 0)
        {
            return trades.Take(limit == default ? maxSize : limit).ToList();
        }
    }
}