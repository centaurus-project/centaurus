using Centaurus.Analytics;
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
        public MarketTickersManager(List<int> markets, OHLCManager framesManager)
        {
            this.framesManager = framesManager ?? throw new ArgumentNullException(nameof(framesManager));
            period = EnumExtensions.GetValues<OHLCFramePeriod>().Min();
            this.markets = markets;
        }

        public async Task Update()
        {
            try
            {
                var updateDate = DateTime.UtcNow;
                await syncRoot.WaitAsync();
                foreach (var market in markets)
                {
                    var currentTicker = tickers.ContainsKey(market) ? tickers[market] : default;
                    var updateTicker = await GenerateTicker(market);
                    if (currentTicker.Equals(updateTicker))
                        updateTicker.UpdatedAt = updateDate;
                    tickers.Add(market, updateTicker);
                }
            }
            finally
            {
                syncRoot.Release();
            }
        }

        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);
        private OHLCFramePeriod period;
        private List<int> markets;
        private OHLCManager framesManager;
        private Dictionary<int, MarketTicker> tickers = new Dictionary<int, MarketTicker>();

        private async Task<MarketTicker> GenerateTicker(int market)
        {
            var frames = await framesManager.GetFrames(0, market, period);
            var fromDate = DateTime.UtcNow.AddDays(-1);
            var framesFor24Hours = frames.frames.TakeWhile(f => f.StartTime >= fromDate);
            var marketTicker = new MarketTicker(market)
            {
                Open = framesFor24Hours.LastOrDefault()?.Open ?? 0,
                Close = framesFor24Hours.FirstOrDefault()?.Close ?? 0,
                High = framesFor24Hours.Select(f => f.High).Max(),
                Low = framesFor24Hours.Select(f => f.Low).Min(),
                BaseAssetVolume = framesFor24Hours.Sum(f => f.BaseAssetVolume),
                MarketAssetVolume = framesFor24Hours.Sum(f => f.MarketAssetVolume)
            };
            return marketTicker;
        }

        public MarketTicker GetMarketTicker(int market)
        {
            try
            {
                syncRoot.Wait();
                if (!tickers.TryGetValue(market, out var ticker))
                    throw new Exception($"Market {market} is not supported.");
                return ticker;
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public List<MarketTicker> GetAllTickers()
        {

            try
            {
                syncRoot.Wait();
                return tickers.Values.ToList();
            }
            finally
            {
                syncRoot.Release();
            }
        }
    }
}