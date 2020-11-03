using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class TradesHistoryManager
    {
        public TradesHistoryManager(List<int> markets, int historySize = 100)
        {
            HistorySize = historySize;
            foreach (var market in markets)
                managers.Add(market, new SingleMarketTradesHistoryManager(market, historySize));
        }

        private Dictionary<int, SingleMarketTradesHistoryManager> managers = new Dictionary<int, SingleMarketTradesHistoryManager>();

        public int HistorySize { get; }

        /// <summary>
        /// Records all trades.
        /// </summary>
        /// <param name="trades"></param>
        /// <returns>Last trades</returns>
        public List<Trade> OnTrade(List<Trade> trades)
        {
            var manager = managers[trades.First().Asset];
            foreach (var trade in trades)
                manager.OnTrade(trade);
            if (trades.Count > HistorySize)
                return manager.GetLastTrades();
            return trades;
        }

        public List<Trade> PullUpdates()
        {
            var updates = new List<Trade>();
            foreach (var manager in managers.Values)
                updates.AddRange(manager.PullUpdates());
            return updates;
        }

        public List<Trade> GetTrades(int market)
        {
            return managers[market].GetLastTrades();
        }
    }
}