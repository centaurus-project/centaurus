using Centaurus.Analytics;
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

namespace Centaurus.Exchange.Analytics
{
    public class OHLCManager : IDisposable
    {
        public OHLCManager(IAnalyticsStorage storage, List<int> markets)
        {
            periods = EnumExtensions.GetValues<OHLCFramePeriod>();
            foreach (var period in periods)
                foreach (var market in markets)
                    managers.Add(EncodeAssetTradesResolution(market, period), new OHLCPeriodManager(period, market, storage));
        }

        public async Task Restore(DateTime dateTime)
        {
            foreach (var manager in managers)
            {
                await manager.Value.Restore(dateTime);
            }
        }

        /// <summary>
        /// Records all trades.
        /// </summary>
        /// <param name="trades"></param>
        /// <returns>Returns updated frames.</returns>
        public async Task OnTrade(int market, List<Trade> trades)
        {
            syncRoot.Wait();
            try
            {
                UpdateManagerFrames(new DateTime(trades.Max(t => t.Timestamp), DateTimeKind.Utc));
                foreach (var period in periods)
                {
                    var managerId = EncodeAssetTradesResolution(market, period);
                    var frameManager = managers[managerId];
                    await frameManager.OnTrade(trades);
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
                while (manager.LastAddedFrame is null || manager.LastAddedFrame.IsExpired(trimmedDate))
                {
                    var nextFrameStartDate = manager.LastAddedFrame?.StartTime.GetNextFrameDate(manager.Period) ?? trimmedDate;
                    var closePrice = manager.LastAddedFrame?.Close ?? 0;
                    var nextFrame = new OHLCFrame(nextFrameStartDate, manager.Period, manager.Market, closePrice);
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
        public async Task<(List<OHLCFrame> frames, int nextCursor)> GetFrames(int cursor, int market, OHLCFramePeriod framePeriod)
        {
            var managerId = EncodeAssetTradesResolution(market, framePeriod);
            var cursorDate = cursor == 0 ? default : DateTimeOffset.FromUnixTimeSeconds(cursor).UtcDateTime;
            var res = await managers[managerId].GetFramesForDate(cursorDate);
            return (
                res.frames,
                nextCursor: (res.nextCursor == default ? 0 : (int)((DateTimeOffset)res.nextCursor).ToUnixTimeSeconds())
                );
        }

        public List<OHLCFrame> PullUpdates()
        {
            var updates = new List<OHLCFrame>();
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
            syncRoot?.Dispose();
            syncRoot = null;
        }

        public static long EncodeAssetTradesResolution(int market, OHLCFramePeriod period)
        {
            return (uint.MaxValue * (long)market) + (int)period;
        }

        public static (int market, OHLCFramePeriod period) DecodeAssetTradesResolution(long managerId)
        {
            return (
                market: (int)Math.Floor(decimal.Divide(managerId, uint.MaxValue)),
                period: (OHLCFramePeriod)(int)(managerId % uint.MaxValue)
            );
        }

        private Dictionary<long, OHLCPeriodManager> managers = new Dictionary<long, OHLCPeriodManager>();
        private IEnumerable<OHLCFramePeriod> periods;
        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);
    }
}