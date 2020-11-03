using Centaurus.Analytics;
using Centaurus.DAL;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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

    public class SinglePeriodOHLCManager : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public SinglePeriodOHLCManager(OHLCFramePeriod period, int market, IAnalyticsStorage analyticsStorage)
        {
            this.analyticsStorage = analyticsStorage;
            this.market = market;
            framesUnit = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromSeconds(15), SizeLimit = 3_000_000 });
            Period = period;
            evictionCallback = new PostEvictionCallbackRegistration()
            {
                EvictionCallback = (subkey, subValue, reason, state) =>
                {
                    if (!locks.TryRemove((DateTime)subkey, out _))
                        logger.Error($"Unable to remove lock for key \"{(DateTime)subkey}\"");
                }
            };
        }
        public OHLCFrame CurrentFrame { get; private set; }

        public void RegisterNewFrame(OHLCFrame frame)
        {
            lock (this)
            {
                var framePeriodDate = GetFramesUnitStart(frame.StartTime); //get current frames unit start date
                if (!framesUnit.TryGetValue<List<OHLCFrame>>(framePeriodDate, out var frames)) //if no unit for the frame, we need to register new one
                {
                    if (CurrentFramesUnitDate != default) //update current cache item entry if it's presented
                    {
                        var currentFrameUnit = framesUnit.Get<List<OHLCFrame>>(CurrentFramesUnitDate);
                        //there is no method to update entry options, so we need to re-register it
                        framesUnit.Remove(CurrentFramesUnitDate);
                        framesUnit.Set(CurrentFramesUnitDate, currentFrameUnit, GetMemoryCacheEntryOptions(false, currentFrameUnit.Count));
                    }
                    //register new frames unit
                    frames = new List<OHLCFrame>();
                    framesUnit.Set(framePeriodDate, frames, GetMemoryCacheEntryOptions(true, FramesPerUnit));
                    CurrentFramesUnitDate = framePeriodDate;
                }
                frames.Add(frame);
                CurrentFrame = frame;
            }
        }

        public void RegisterTrade(Trade trade)
        {
            CurrentFrame.RegisterTrade(trade);
            updates.AddUpdate(CurrentFramesUnitDate, CurrentFrame);
        }

        public IEnumerable<OHLCFrame> PullUpdates()
        {
            return updates.PullUpdates();
        }

        private UpdateContainer<DateTime, OHLCFrame> updates = new UpdateContainer<DateTime, OHLCFrame>();

        private MemoryCacheEntryOptions GetMemoryCacheEntryOptions(bool isCurrentEntry, int size)
        {
            MemoryCacheEntryOptions memoryCache;
            if (isCurrentEntry)
                memoryCache = new MemoryCacheEntryOptions
                {
                    Size = size,
                    Priority = CacheItemPriority.NeverRemove
                };

            else
                memoryCache = new MemoryCacheEntryOptions
                {
                    Size = size,
                    Priority = CacheItemPriority.Low,
                    SlidingExpiration = TimeSpan.FromMinutes(10)
                };
            memoryCache.PostEvictionCallbacks.Add(evictionCallback);
            return memoryCache;
        }

        private PostEvictionCallbackRegistration evictionCallback;

        private DateTime CurrentFramesUnitDate;
        private MemoryCache framesUnit;

        private readonly IAnalyticsStorage analyticsStorage;
        private readonly int market;

        private readonly ConcurrentDictionary<DateTime, SemaphoreSlim> locks = new ConcurrentDictionary<DateTime, SemaphoreSlim>();

        private int FramesPerUnit
        {
            get
            {
                switch (Period)
                {
                    case OHLCFramePeriod.Minute:
                    case OHLCFramePeriod.Hour:
                    case OHLCFramePeriod.Day:
                        return 1000;
                    case OHLCFramePeriod.Week:
                        return 100;
                    case OHLCFramePeriod.Month:
                        return 12;
                    default:
                        throw new NotSupportedException($"{Period} is not supported yet.");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dateTime">Trimmed date</param>
        /// <param name="period"></param>
        /// <returns></returns>
        private DateTime GetFramesUnitStart(DateTime dateTime)
        {
            switch (Period)
            {
                case OHLCFramePeriod.Minute:
                case OHLCFramePeriod.Hour:
                case OHLCFramePeriod.Day:
                case OHLCFramePeriod.Week:
                    var dateTicks = dateTime.Ticks / TicksPerPeriod;
                    var periodStartDateTicks = dateTicks - dateTicks % FramesPerUnit;
                    return new DateTime(periodStartDateTicks * TicksPerPeriod, dateTime.Kind);
                case OHLCFramePeriod.Month:
                    return new DateTime(dateTime.Year, 1, 1, 0, 0, 0, dateTime.Kind);
                default:
                    throw new NotSupportedException($"{Period} is not supported.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dateTime">Current frames unit start</param>
        /// <param name="period"></param>
        /// <returns></returns>
        private DateTime GetNextFrameStart(DateTime dateTime)
        {
            switch (Period)
            {
                case OHLCFramePeriod.Minute:
                case OHLCFramePeriod.Hour:
                case OHLCFramePeriod.Day:
                case OHLCFramePeriod.Week:
                    return dateTime.AddTicks(TicksPerPeriod * FramesPerUnit);
                case OHLCFramePeriod.Month:
                    return dateTime.AddYears(1);
                default:
                    throw new NotSupportedException($"{Period} is not supported.");
            }
        }

        const long TicksPerWeek = TimeSpan.TicksPerDay * 7;

        private long TicksPerPeriod
        {
            get
            {
                switch (Period)
                {
                    case OHLCFramePeriod.Minute:
                        return TimeSpan.TicksPerMinute;
                    case OHLCFramePeriod.Hour:
                        return TimeSpan.TicksPerHour;
                    case OHLCFramePeriod.Day:
                        return TimeSpan.TicksPerDay;
                    case OHLCFramePeriod.Week:
                        return TicksPerWeek;
                    default:
                        throw new InvalidOperationException($"{Period} doesn't support ticks.");
                }
            }
        }

        public OHLCFramePeriod Period { get; }

        private async Task<List<OHLCFrame>> GetUnit(DateTime unitDate)
        {
            if (!framesUnit.TryGetValue<List<OHLCFrame>>(unitDate, out var frames))
            {
                var semaphore = locks.GetOrAdd(unitDate, (d) => new SemaphoreSlim(1));
                await semaphore.WaitAsync();
                try
                {
                    if (!framesUnit.TryGetValue(unitDate, out frames))
                    {
                        var unixTimeStamp = (int)((DateTimeOffset)unitDate).ToUnixTimeSeconds();
                        var period = await analyticsStorage.GetFrames(unixTimeStamp, market, Period, FramesPerUnit);
                        framesUnit.Set(unitDate, period, GetMemoryCacheEntryOptions(false, period.Count));
                        frames = period.Cast<OHLCFrame>().ToList();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            return frames;
        }

        public async Task<List<OHLCFrame>> GetPeriod(DateTime from, DateTime to)
        {
            var fetchTasks = new List<Task<List<OHLCFrame>>>();
            var fromPeriod = GetFramesUnitStart(from);
            var toPeriod = GetFramesUnitStart(to);
            if (fromPeriod >= CurrentFramesUnitDate)
                return new List<OHLCFrame>();
            do
            {
                fetchTasks.Add(GetUnit(fromPeriod));
                fromPeriod = GetNextFrameStart(fromPeriod);
            }
            while (toPeriod > fromPeriod || toPeriod >= CurrentFramesUnitDate);

            await Task.WhenAll(fetchTasks);

            return fetchTasks.SelectMany(t => t.Result).ToList();
        }

        public void Dispose()
        {
            framesUnit?.Dispose();
            framesUnit = null;
        }
    }
}
