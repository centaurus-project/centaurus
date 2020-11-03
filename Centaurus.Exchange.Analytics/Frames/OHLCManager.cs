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
                    frameManager.RegisterTrade(trade);
                    if (!updatedFrames[period].Contains(frameManager.CurrentFrame))
                        updatedFrames[period].Add(frameManager.CurrentFrame);
                }
            }
            return updatedFrames;
        }

        public async Task<IEnumerable<OHLCFrame>> GetPeriod(DateTime from, DateTime to, int market, OHLCFramePeriod framePeriod)
        {
            if (to <= from)
                throw new Exception("Specified period is invalid.");

            from = from.Trim(framePeriod);
            to = to.Trim(framePeriod);
            var managerId = EncodeManagerId(market, framePeriod);
            var frames = await managers[managerId].GetPeriod(from, to);
            return frames.SkipWhile(f => f.StartTime < from).TakeWhile(f => f.StartTime < to);
        }

        public List<OHLCFrame> GetAllCurrentFrames(int market)
        {
            var currentFrames = new List<OHLCFrame>();
            foreach (var period in periods)
            {
                var managerId = EncodeManagerId(market, period);
                currentFrames.Add(managers[managerId].CurrentFrame);
            }
            return currentFrames;
        }

        public List<OHLCFrame> PullUpdates()
        {
            var updates = new List<OHLCFrame>();
            foreach (var manager in managers.Values)
                updates.AddRange(manager.PullUpdates());
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
