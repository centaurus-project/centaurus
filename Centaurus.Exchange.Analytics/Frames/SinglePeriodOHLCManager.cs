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
            this.analyticsStorage = analyticsStorage ?? throw new ArgumentNullException(nameof(analyticsStorage));
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

        public async Task Restore(DateTime dateTime)
        {
            CurrentFramesUnitDate = GetFramesUnitDate(dateTime);
            var frames = await GetUnit(CurrentFramesUnitDate, true);
            CurrentFrame = frames.FirstOrDefault();

            var firstFramesDate = (await analyticsStorage.GetFirstFrameDate(Period));
            if (firstFramesDate == 0)
                firstFramesUnitDate = GetFramesUnitDate(DateTimeOffset.FromUnixTimeSeconds(firstFramesDate).DateTime);
        }

        public OHLCFrame CurrentFrame { get; private set; }

        public void RegisterNewFrame(OHLCFrame frame)
        {
            var unitDate = GetFramesUnitDate(frame.StartTime); //get current frames unit start date

            var semaphore = locks.GetOrAdd(unitDate, (d) => new SemaphoreSlim(1));
            semaphore.Wait();
            try
            {
                if (!framesUnit.TryGetValue<List<OHLCFrame>>(unitDate, out var frames)) //if no unit for the frame, we need to register new one
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
                    framesUnit.Set(unitDate, frames, GetMemoryCacheEntryOptions(true, FramesPerUnit));
                    CurrentFramesUnitDate = unitDate;
                    if (firstFramesUnitDate == default)
                        firstFramesUnitDate = CurrentFramesUnitDate;
                }
                if (frame.StartTime < CurrentFramesUnitDate)
                { }
                frames.Insert(0, frame);
                CurrentFrame = frame;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void OnTrade(Trade trade)
        {
            CurrentFrame.OnTrade(trade);
            updates.AddUpdate(CurrentFrame.StartTime, CurrentFrame);
        }

        public List<OHLCFrame> PullUpdates()
        {
            return updates.PullUpdates();
        }

        public async Task<(List<OHLCFrame> frames, DateTime nextCursor)> GetFramesForDate(DateTime cursor)
        {
            if (cursor == default)
                cursor = CurrentFrame?.StartTime ?? default;

            var fromPeriod = GetFramesUnitDate(cursor);
            if (fromPeriod > CurrentFramesUnitDate && CurrentFramesUnitDate != default)
                fromPeriod = CurrentFramesUnitDate;

            var frames = await GetUnit(fromPeriod);
            return (
                frames,
                nextCursor: (HasMore(fromPeriod) ? GetFramesNextUnitStart(fromPeriod, true) : default)
                );
        }

        public void Dispose()
        {
            framesUnit?.Dispose();
            framesUnit = null;
            if (locks != null)
            {
                foreach (var @lock in locks)
                    @lock.Value.Dispose();
                locks = null;
            }
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

        private DateTime firstFramesUnitDate;

        private ConcurrentDictionary<DateTime, SemaphoreSlim> locks = new ConcurrentDictionary<DateTime, SemaphoreSlim>();

        private int FramesPerUnit
        {
            get
            {
                switch (Period)
                {
                    case OHLCFramePeriod.Week:
                        return 100;
                    case OHLCFramePeriod.Month:
                        return 12;
                    default:
                        return 1000;
                }
            }
        }

        /// <summary>
        /// Finds current date unit start date
        /// </summary>
        /// <param name="dateTime">Trimmed date</param>
        /// <param name="period"></param>
        /// <returns></returns>
        private DateTime GetFramesUnitDate(DateTime dateTime)
        {
            switch (Period)
            {
                case OHLCFramePeriod.Month:
                    return new DateTime(dateTime.Year, 1, 1, 0, 0, 0, dateTime.Kind);
                default:
                    var dateTicks = dateTime.Ticks / TicksPerPeriod;
                    var unitReminder = dateTicks % FramesPerUnit;
                    var periodStartDateTicks = dateTicks - unitReminder;
                    return new DateTime(periodStartDateTicks * TicksPerPeriod, dateTime.Kind);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dateTime">Current frames unit start</param>
        /// <param name="period"></param>
        /// <returns></returns>
        private DateTime GetFramesNextUnitStart(DateTime dateTime, bool inverse = false)
        {
            var direction = inverse ? -1 : 1;
            switch (Period)
            {
                case OHLCFramePeriod.Month:
                    return dateTime.AddYears(1 * direction);
                default:
                    return dateTime.AddTicks((TicksPerPeriod * FramesPerUnit) * direction);
            }
        }

        public OHLCFramePeriod Period { get; }
        private long TicksPerPeriod => OHLCPeriodHelper.TicksPerPeriod(Period);

        private async Task<List<OHLCFrame>> GetUnit(DateTime unitDate, bool isCurrentFrame = false)
        {
            if (!framesUnit.TryGetValue<List<OHLCFrame>>(unitDate, out var frames))
            {
                var semaphore = locks.GetOrAdd(unitDate, (d) => new SemaphoreSlim(1));
                await semaphore.WaitAsync();
                try
                {
                    if (!framesUnit.TryGetValue(unitDate, out frames))
                    {
                        var fromTimeStamp = (int)((DateTimeOffset)GetFramesNextUnitStart(unitDate)).ToUnixTimeSeconds();
                        var unixTimeStamp = (int)((DateTimeOffset)unitDate).ToUnixTimeSeconds();
                        var rawFrames = await analyticsStorage.GetFrames(fromTimeStamp, unixTimeStamp, market, Period);
                        frames = rawFrames.Select(f => f.FromModel()).ToList();
                        framesUnit.Set(unitDate, frames, GetMemoryCacheEntryOptions(isCurrentFrame, frames.Count));
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            return frames;
        }

        private bool HasMore(DateTime fromPeriod)
        {
            return firstFramesUnitDate != default && firstFramesUnitDate < fromPeriod;
        }
    }
}
