using Centaurus.Models;
using Centaurus.DAL;
using Centaurus.DAL.Models.Analytics;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Centaurus.Exchange.Analytics
{
    public class PriceHistoryManager : IDisposable
    {
        public PriceHistoryManager(IAnalyticsStorage storage, List<int> markets)
        {
            periods = Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>();
            foreach (var period in periods)
                foreach (var market in markets)
                    managers.Add(EncodeAssetTradesResolution(market, period), new PriceHistoryPeriodManager(period, market, storage));
        }

        public async Task Restore(DateTime dateTime)
        {
            foreach (var manager in managers)
            {
                await manager.Value.Restore(dateTime);
            }
        }

        public async Task OnTrade(ExchangeUpdate exchangeUpdate)
        {
            if (exchangeUpdate == null)
                throw new ArgumentNullException(nameof(exchangeUpdate));

            var trades = exchangeUpdate.Trades;
            if (trades == null || trades.Count < 1)
                return;

            syncRoot.Wait();
            try
            {
                UpdateManagerFrames(exchangeUpdate.UpdateDate);
                foreach (var period in periods)
                {
                    var managerId = EncodeAssetTradesResolution(exchangeUpdate.Market, period);
                    var frameManager = managers[managerId];
                    await frameManager.OnTrade(exchangeUpdate);
                }
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public void Update()
        {
            syncRoot.Wait();
            try
            {
                UpdateManagerFrames(DateTime.UtcNow);
            }
            finally
            {
                syncRoot.Release();
            }
        }

        private void UpdateManagerFrames(DateTime currentDate)
        {
            foreach (var manager in managers.Values)
            {
                var trimmedDate = currentDate.Trim(manager.Period);
                while (manager.LastAddedFrame is null 
                    || manager.LastAddedFrame.IsExpired(trimmedDate))
                {
                    var nextFrameStartDate = manager.LastAddedFrame?.StartTime.GetNextFrameDate(manager.Period) ?? trimmedDate;
                    var closePrice = manager.LastAddedFrame?.Close ?? 0;
                    var nextFrame = new PriceHistoryFrame(nextFrameStartDate, manager.Period, manager.Market, closePrice);
                    manager.RegisterNewFrame(nextFrame);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cursor">Unix timestamp</param>
        /// <param name="market"></param>
        /// <param name="framePeriod"></param>
        /// <returns></returns>
        public async Task<(List<PriceHistoryFrame> frames, int nextCursor)> GetPriceHistory(int cursor, int market, PriceHistoryPeriod framePeriod)
        {
            var managerId = EncodeAssetTradesResolution(market, framePeriod);
            var cursorDate = cursor == 0 ? default : DateTimeOffset.FromUnixTimeSeconds(cursor).UtcDateTime;
            var res = await managers[managerId].GetPriceHistoryForDate(cursorDate);
            return (
                res.frames,
                nextCursor: (res.nextCursor == default ? 0 : (int)((DateTimeOffset)res.nextCursor).ToUnixTimeSeconds())
            );
        }

        public List<PriceHistoryFrame> PullUpdates()
        {
            var updates = new List<PriceHistoryFrame>();
            foreach (var manager in managers.Values)
            {
                var updateData = manager.PullUpdates();
                if (updateData.Count < 1)
                    continue;
                updates.AddRange(updateData);
            }
            return updates;
        }

        public void Dispose()
        {
            foreach (var m in managers)
                m.Value.Dispose();
            syncRoot.Dispose();
        }

        public static long EncodeAssetTradesResolution(int market, PriceHistoryPeriod period)
        {
            return (uint.MaxValue * (long)market) + (int)period;
        }

        public static (int market, PriceHistoryPeriod period) DecodeAssetTradesResolution(long managerId)
        {
            return (
                market: (int)Math.Floor(decimal.Divide(managerId, uint.MaxValue)),
                period: (PriceHistoryPeriod)(int)(managerId % uint.MaxValue)
            );
        }

        private readonly Dictionary<long, PriceHistoryPeriodManager> managers = new Dictionary<long, PriceHistoryPeriodManager>();
        private IEnumerable<PriceHistoryPeriod> periods;
        private readonly SemaphoreSlim syncRoot = new SemaphoreSlim(1);
    }
}