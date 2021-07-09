using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class TradesHistoryManager
    {
        public TradesHistoryManager(List<string> markets, int historySize = 100)
        {
            HistorySize = historySize;
            foreach (var market in markets)
                managers.Add(market, new MarketTradesHistoryManager(market, historySize));
        }

        private Dictionary<string, MarketTradesHistoryManager> managers = new Dictionary<string, MarketTradesHistoryManager>();

        public int HistorySize { get; }

        public void OnTrade(ExchangeUpdate exchangeUpdate)
        {
            if (exchangeUpdate == null)
                throw new ArgumentNullException(nameof(exchangeUpdate));
            if (exchangeUpdate.Trades == null)
                throw new ArgumentNullException(nameof(exchangeUpdate.Trades));

            var market = exchangeUpdate.Market;
            var trades = exchangeUpdate.Trades;
            var updateDate = exchangeUpdate.UpdateDate;

            lock (managers)
            {
                if (!managers.TryGetValue(market, out var tradesHistoryManager))
                    throw new ArgumentException($"Market {market} is not supported.");
                tradesHistoryManager.OnTrade(trades, updateDate);
            }
        }

        public List<Trade> GetTrades(string market)
        {
            lock (managers)
            {
                if (!managers.TryGetValue(market, out var tradesHistoryManager))
                    throw new ArgumentException($"Market {market} is not supported.");
                return tradesHistoryManager.GetLastTrades();
            }
        }
    }
}