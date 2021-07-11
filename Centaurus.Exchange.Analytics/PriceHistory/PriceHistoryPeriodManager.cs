using Centaurus.Models;
using Centaurus.PersistentStorage.Abstraction;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Exchange.Analytics
{

    public class PriceHistoryPeriodManager : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public PriceHistoryPeriodManager(PriceHistoryPeriod period, string market, IPersistentStorage analyticsStorage)
        {
            this.analyticsStorage = analyticsStorage ?? throw new ArgumentNullException(nameof(analyticsStorage));
            Market = market;
            framesUnit = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromSeconds(15), SizeLimit = 3_000_000 });
            Period = period;
            evictionCallback = new PostEvictionCallbackRegistration()
            {
                EvictionCallback = (subkey, subValue, reason, state) =>
                {
                    if (!syncRoots.TryRemove((DateTime)subkey, out _))
                        logger.Error($"Unable to remove lock for key \"{(DateTime)subkey}\"");
                }
            };
        }
        public string Market { get; }

        public void Restore(DateTime dateTime)
        {
            CurrentFramesUnitDate = GetFramesUnitDate(dateTime);
            var frames = GetUnit(CurrentFramesUnitDate, true);
            LastAddedFrame = frames.FirstOrDefault();

            var firstFrame = analyticsStorage.GetPriceHistory(0, int.MaxValue, (int)Period, Market).FirstOrDefault();
            if (firstFrame != null)
                firstFramesUnitDate = GetFramesUnitDate(DateTimeOffset.FromUnixTimeSeconds(firstFrame.Timestamp).DateTime);
        }

        public PriceHistoryFrame LastAddedFrame { get; private set; }

        public DateTime LastUpdated { get; private set; }

        public void RegisterNewFrame(PriceHistoryFrame frame)
        {
            var unitDate = GetFramesUnitDate(frame.StartTime); //get current frames unit start date

            var syncRoot = GetSyncRoot(unitDate);
            lock(syncRoot)
            {
                if (!framesUnit.TryGetValue<List<PriceHistoryFrame>>(unitDate, out var frames)) //if no unit for the frame, we need to register new one
                {
                    if (CurrentFramesUnitDate != default) //update current cache item entry if it's presented
                    {
                        var currentFrameUnit = framesUnit.Get<List<PriceHistoryFrame>>(CurrentFramesUnitDate);
                        //there is no method to update entry options, so we need to re-register it
                        framesUnit.Remove(CurrentFramesUnitDate);
                        framesUnit.Set(CurrentFramesUnitDate, currentFrameUnit, GetMemoryCacheEntryOptions(false, currentFrameUnit.Count));
                    }
                    //register new frames unit
                    frames = new List<PriceHistoryFrame>();
                    framesUnit.Set(unitDate, frames, GetMemoryCacheEntryOptions(true, FramesPerUnit));
                    CurrentFramesUnitDate = unitDate;
                    if (firstFramesUnitDate == default)
                        firstFramesUnitDate = CurrentFramesUnitDate;
                }
                frames.Insert(0, frame);
                updates.AddUpdate(frame.StartTime, frame);
                LastAddedFrame = frame;
            }
        }

        public void OnTrade(ExchangeUpdate exchangeUpdate)
        {
            var tradeDate = exchangeUpdate.UpdateDate.Trim(Period);
            var frame = GetFrame(tradeDate);
            if (frame == null)
                throw new Exception($"Unable to find frame for date time {tradeDate}.");

            foreach (var trade in exchangeUpdate.Trades)
            {
                frame.OnTrade(trade, tradeDate);
                updates.AddUpdate(frame.StartTime, frame);
            }
        }

        public List<PriceHistoryFrame> PullUpdates()
        {
            return updates.PullUpdates();
        }

        public (List<PriceHistoryFrame> frames, DateTime nextCursor) GetPriceHistoryForDate(DateTime cursor)
        {
            if (cursor == default)
                cursor = LastAddedFrame?.StartTime ?? default;

            var fromPeriod = GetFramesUnitDate(cursor);
            if (fromPeriod > CurrentFramesUnitDate && CurrentFramesUnitDate != default)
                fromPeriod = CurrentFramesUnitDate;

            var frames = GetUnit(fromPeriod);
            return (
                frames,
                nextCursor: (HasMore(fromPeriod) ? GetFramesNextUnitStart(fromPeriod, true) : default)
                );
        }

        public void Dispose()
        {
            framesUnit.Dispose();
        }

        private UpdateContainer<DateTime, PriceHistoryFrame> updates = new UpdateContainer<DateTime, PriceHistoryFrame>();

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

        private readonly IPersistentStorage analyticsStorage;

        private DateTime firstFramesUnitDate;

        private readonly ConcurrentDictionary<DateTime, object> syncRoots = new ConcurrentDictionary<DateTime, object>();
        private object GetSyncRoot(DateTime unitDate)
        {
            return syncRoots.GetOrAdd(unitDate, (d) => new {});
        }

        private int FramesPerUnit
        {
            get
            {
                switch (Period)
                {
                    case PriceHistoryPeriod.Week:
                        return 100;
                    case PriceHistoryPeriod.Month:
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
                case PriceHistoryPeriod.Month:
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
                case PriceHistoryPeriod.Month:
                    return dateTime.AddYears(1 * direction);
                default:
                    return dateTime.AddTicks((TicksPerPeriod * FramesPerUnit) * direction);
            }
        }

        public PriceHistoryPeriod Period { get; }
        private long TicksPerPeriod => PriceHistoryPeriodHelper.TicksPerPeriod(Period);

        private List<PriceHistoryFrame> GetUnit(DateTime unitDate, bool isCurrentFrame = false)
        {
            if (!framesUnit.TryGetValue<List<PriceHistoryFrame>>(unitDate, out var frames))
            {
                var syncRoot = GetSyncRoot(unitDate);
                lock(syncRoot)
                { 
                    if (!framesUnit.TryGetValue(unitDate, out frames))
                    {
                        var nextUnitDate = GetFramesNextUnitStart(unitDate);
                        var toTimeStamp = (int)((DateTimeOffset)nextUnitDate).ToUnixTimeSeconds();
                        var unixTimeStamp = (int)((DateTimeOffset)unitDate).ToUnixTimeSeconds();
                        var rawFrames = analyticsStorage.GetPriceHistory(unixTimeStamp, toTimeStamp, (int)Period, Market);

                        frames = rawFrames.Select(f => f.FromFramePersistentModel()).OrderByDescending(f => f.StartTime).ToList();


                        foreach (var f in frames)
                        {
                            if (f.StartTime < unitDate || f.StartTime >= nextUnitDate || f.Market != Market)
                                break;
                        }
                        framesUnit.Set(unitDate, frames, GetMemoryCacheEntryOptions(isCurrentFrame, frames.Count));
                    }
                }
            }
            return frames;
        }

        private PriceHistoryFrame GetFrame(DateTime dateTime)
        {
            var unitDate = GetFramesUnitDate(dateTime);
            var unit = GetUnit(unitDate);
            var currentDateFrame = unit?.FirstOrDefault(f => f.StartTime == dateTime);
            return currentDateFrame;
        }

        private bool HasMore(DateTime fromPeriod)
        {
            return firstFramesUnitDate != default && firstFramesUnitDate < fromPeriod;
        }
    }
}
