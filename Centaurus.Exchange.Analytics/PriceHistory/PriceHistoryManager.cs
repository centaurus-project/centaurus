using Centaurus.Models;
using Centaurus.PersistentStorage.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Exchange.Analytics
{
    public class PriceHistoryManager : IDisposable
    {
        public PriceHistoryManager(IPersistentStorage storage, List<string> markets)
        {
            periods = Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>();
            foreach (var period in periods)
                foreach (var market in markets)
                    managers.Add(EncodeAssetTradesResolution(market, period), new PriceHistoryPeriodManager(period, market, storage));
        }

        public void Restore(DateTime dateTime)
        {
            foreach (var manager in managers)
            {
                manager.Value.Restore(dateTime);
            }
        }

        public void OnTrade(ExchangeUpdate exchangeUpdate)
        {
            if (exchangeUpdate == null)
                throw new ArgumentNullException(nameof(exchangeUpdate));

            var trades = exchangeUpdate.Trades;
            if (trades == null || trades.Count < 1)
                return;

            lock (syncRoot)
            {
                UpdateManagerFrames(exchangeUpdate.UpdateDate);
                foreach (var period in periods)
                {
                    var managerId = EncodeAssetTradesResolution(exchangeUpdate.Market, period);
                    var frameManager = managers[managerId];
                    frameManager.OnTrade(exchangeUpdate);
                }
            }
        }

        public void Update()
        {
            lock (syncRoot)
                UpdateManagerFrames(DateTime.UtcNow);
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
        public (List<PriceHistoryFrame> frames, int nextCursor) GetPriceHistory(int cursor, string market, PriceHistoryPeriod framePeriod)
        {
            var managerId = EncodeAssetTradesResolution(market, framePeriod);
            var cursorDate = cursor == 0 ? default : DateTimeOffset.FromUnixTimeSeconds(cursor).UtcDateTime;
            var res = managers[managerId].GetPriceHistoryForDate(cursorDate);
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
        }

        public static string EncodeAssetTradesResolution(string market, PriceHistoryPeriod period)
        {
            return $"{market}_{(int)period}";
        }

        public static (string market, PriceHistoryPeriod period) DecodeAssetTradesResolution(string managerId)
        {
            var splitted = managerId.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (splitted.Length != 2 || !int.TryParse(splitted[1], out var period))
                throw new ArgumentException($"{managerId} is invalid format.");
            return (
                market: splitted[0],
                period: (PriceHistoryPeriod)period
            );
        }

        private readonly Dictionary<string, PriceHistoryPeriodManager> managers = new Dictionary<string, PriceHistoryPeriodManager>();
        private IEnumerable<PriceHistoryPeriod> periods;
        private readonly object syncRoot = new { };
    }
}