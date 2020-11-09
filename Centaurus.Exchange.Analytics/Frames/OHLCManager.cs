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
            periods = Enum.GetValues(typeof(OHLCFramePeriod)).Cast<OHLCFramePeriod>();
            foreach (var period in periods)
                foreach (var market in markets)
                    managers.Add(EncodeManagerId(market, period), new SinglePeriodOHLCManager(period, market, storage));
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
        public Dictionary<OHLCFramePeriod, List<OHLCFrame>> OnTrade(List<Trade> trades)
        {
            var updatedFrames = periods.ToDictionary(p => p, p => new List<OHLCFrame>());
            foreach (var trade in trades)
            {
                var tradeDateTime = new DateTime(trade.Timestamp, DateTimeKind.Utc);
                foreach (var period in periods)
                {
                    var managerId = EncodeManagerId(trade.Asset, period);
                    var frameManager = managers[managerId];

                    var trimmedDateTime = tradeDateTime.Trim(frameManager.Period);
                    if (frameManager.CurrentFrame is null || frameManager.CurrentFrame.IsExpired(trimmedDateTime))
                    {
                        frameManager.RegisterNewFrame(new OHLCFrame(trimmedDateTime, frameManager.Period, trade.Asset));
                    }
                    frameManager.OnTrade(trade);
                    if (!updatedFrames[period].Contains(frameManager.CurrentFrame))
                        updatedFrames[period].Add(frameManager.CurrentFrame);
                }
            }
            return updatedFrames;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cursor">Unix timestamp</param>
        /// <param name="market"></param>
        /// <param name="framePeriod"></param>
        /// <returns></returns>
        public async Task<(List<OHLCFrame> frames, int nextCursor)> GetPeriod(int cursor, int market, OHLCFramePeriod framePeriod)
        {
            var managerId = EncodeManagerId(market, framePeriod);
            var cursorDate = cursor == 0 ? default : DateTimeOffset.FromUnixTimeSeconds(cursor).DateTime;
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
        }

        public static long EncodeManagerId(int market, OHLCFramePeriod period)
        {
            return (uint.MaxValue * (long)market) + (int)period;
        }

        public static (int market, OHLCFramePeriod period) DecodeManagerId(long managerId)
        {
            return (
                market: (int)Math.Floor(decimal.Divide(managerId, uint.MaxValue)),
                period: (OHLCFramePeriod)(int)(managerId % uint.MaxValue)
            );
        }

        private Dictionary<long, SinglePeriodOHLCManager> managers = new Dictionary<long, SinglePeriodOHLCManager>();
        private IEnumerable<OHLCFramePeriod> periods;
    }
}
