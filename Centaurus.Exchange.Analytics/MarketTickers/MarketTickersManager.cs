using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Exchange.Analytics
{
    public class MarketTickersManager
    {
        public MarketTickersManager(List<string> markets, PriceHistoryManager framesManager)
        {
            this.framesManager = framesManager ?? throw new ArgumentNullException(nameof(framesManager));
            period = Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>().Min();
            this.markets = markets;
        }

        public void Update()
        {
            var updateDate = DateTime.UtcNow;
            lock (syncRoot)
                foreach (var market in markets)
                {
                    if (!tickers.TryGetValue(market, out var currentTicker))
                    {
                        currentTicker = new MarketTicker(market);
                        tickers.Add(market, currentTicker);
                    }
                    UpdateTicker(currentTicker, updateDate);
                }
        }

        private object syncRoot = new { };
        private PriceHistoryPeriod period;
        private List<string> markets;
        private PriceHistoryManager framesManager;
        private Dictionary<string, MarketTicker> tickers = new Dictionary<string, MarketTicker>();

        private void UpdateTicker(MarketTicker marketTicker, DateTime updateDate)
        {
            var frames = framesManager.GetPriceHistory(0, marketTicker.Market, period);
            var fromDate = DateTime.UtcNow.AddDays(-1);
            var framesFor24Hours = frames.frames.TakeWhile(f => f.StartTime >= fromDate);
            if (framesFor24Hours.Count() < 1)
                return;

            marketTicker.Open = framesFor24Hours.Last().Open;
            marketTicker.Close = framesFor24Hours.First().Close;
            marketTicker.High = framesFor24Hours.Select(f => f.High).Max();
            marketTicker.Low = framesFor24Hours.Select(f => f.Low).Min();
            marketTicker.BaseVolume = framesFor24Hours.Sum(f => f.BaseVolume);
            marketTicker.CounterVolume = framesFor24Hours.Sum(f => f.CounterVolume);
            marketTicker.UpdatedAt = updateDate;
        }

        public MarketTicker GetMarketTicker(string market)
        {
            lock (syncRoot)
            {
                if (!tickers.TryGetValue(market, out var ticker))
                    throw new Exception($"Market {market} is not supported.");
                return ticker;
            }
        }

        public List<MarketTicker> GetAllTickers()
        {
            lock (syncRoot)
                return tickers.Values.ToList();
        }
    }
}