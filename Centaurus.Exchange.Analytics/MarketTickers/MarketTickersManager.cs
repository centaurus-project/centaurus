using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Exchange.Analytics
{
    public class MarketTickersManager
    {
        public MarketTickersManager(OHLCManager framesManager)
        {
            this.frameManagers = frameManagers ?? throw new ArgumentNullException(nameof(frameManagers));
            period = EnumExtensions.GetValues<OHLCFramePeriod>().Min();
        }

        private OHLCManager frameManagers;
        private OHLCFramePeriod period;

        public async Task<SingleMarketTicker> UpdateTicker(int market)
        {
            var frames = await frameManagers.GetPeriod(0, market, period);
            var fromDate = DateTime.UtcNow.AddDays(-1);
            var framesFor24Hours = frames.frames.TakeWhile(f => f.StartTime >= fromDate);
            var marketTicker = new SingleMarketTicker(market)
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
    }
}
