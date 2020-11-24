using Centaurus.Models;
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
                managers.Add(market, new MarketTradesHistoryManager(market, historySize));
        }

        private Dictionary<int, MarketTradesHistoryManager> managers = new Dictionary<int, MarketTradesHistoryManager>();

        public int HistorySize { get; }

        /// <summary>
        /// Records all trades.
        /// </summary>
        /// <param name="trades"></param>
        /// <returns>Last trades</returns>
        public void OnTrade(int market, List<Trade> trades)
        {
            if (!managers.ContainsKey(market))
                throw new ArgumentException($"Market {market} is not supported.");
            if (trades == null)
                throw new ArgumentNullException(nameof(trades));

            lock (managers)
                managers[market].OnTrade(trades);
        }

        public List<Trade> GetTrades(int market)
        {
            if (!managers.ContainsKey(market))
                throw new ArgumentException($"Market {market} is not supported.");
            lock (managers)
                return managers[market].GetLastTrades();
        }
    }
}