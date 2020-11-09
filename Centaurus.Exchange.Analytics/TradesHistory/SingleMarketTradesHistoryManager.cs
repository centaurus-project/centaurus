using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class SingleMarketTradesHistoryManager
    {
        public SingleMarketTradesHistoryManager(int market, int maxSize = 100)
        {
            Market = market;
            this.maxSize = maxSize;
        }

        public int Market { get; }

        private int maxSize;
        private LinkedList<Trade> trades = new LinkedList<Trade>();

        public void OnTrade(Trade trade)
        {
            trades.AddFirst(trade);
            if (trades.Count > maxSize)
                trades.RemoveLast();
        }

        public List<Trade> GetLastTrades(int limit = 0)
        {
            return trades.Take(limit == default ? maxSize : limit).ToList();
        }
    }
}