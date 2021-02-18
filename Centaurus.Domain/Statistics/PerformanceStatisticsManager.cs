using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    public class PerformanceStatisticsManager: IDisposable
    {
        const int updateInterval = 1000;

        static Logger logger = LogManager.GetCurrentClassLogger();

        public PerformanceStatisticsManager()
        {
            updateTimer = new System.Timers.Timer();
            updateTimer.Interval = updateInterval;
            updateTimer.AutoReset = false;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }

        public event Action<PerformanceStatisticsManagerUpdate> OnUpdates;

        public void OnBatchSaved(PendingUpdatesManager.BatchSavedInfo batchInfo)
        {
            lock (syncRoot)
            {
                LastBatchInfos.Add(batchInfo);
                if (LastApexes.Count > 5)
                    LastApexes.RemoveAt(0);
            }
        }

        private List<Apex> LastApexes = new List<Apex>();
        private List<PendingUpdatesManager.BatchSavedInfo> LastBatchInfos = new List<PendingUpdatesManager.BatchSavedInfo>();

        private object syncRoot = new { };
        private Timer updateTimer;

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (syncRoot)
            {
                try
                {
                    var quantaPerSecond = GetItemsPerSecond();
                    var throttling = GetThrottling();
                    OnUpdates?.Invoke(new PerformanceStatisticsManagerUpdate { BatchInfos = LastBatchInfos, QuantaPerSecond = quantaPerSecond, Trottling = throttling });
                    updateTimer?.Start();
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                }
            }
        }

        private int GetThrottling()
        {
            return QuantaThrottlingManager.Current.IsThrottlingEnabled ? QuantaThrottlingManager.Current.MaxItemsPerSecond : 0;
        }

        private int GetItemsPerSecond()
        {
            LastApexes.Add(new Apex { UpdatedAt = DateTime.UtcNow, CurrentApex = Global.QuantumStorage.CurrentApex });
            if (LastApexes.Count > 5)
                LastApexes.RemoveAt(0);

            if (LastApexes.Count < 2) //intervals for 
                return 0;
            var lastItem = LastApexes.Last();
            var firstItem = LastApexes.First();
            var timeDiff = (decimal)(lastItem.UpdatedAt - firstItem.UpdatedAt).TotalMilliseconds;
            return (int)(decimal.Divide(lastItem.CurrentApex - firstItem.CurrentApex, timeDiff) * 1000);
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                updateTimer?.Stop();
                updateTimer?.Dispose();
                updateTimer = null;
            }
        }

        public class PerformanceStatisticsManagerUpdate
        {
            public int QuantaPerSecond { get; set; }

            public int Trottling { get; set; }

            public List<PendingUpdatesManager.BatchSavedInfo> BatchInfos { get; set; }
        }

        class Apex
        {
            public long CurrentApex { get; set; }

            public DateTime UpdatedAt { get; set; }
        }
    }
}
