using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    public abstract class PerformanceStatisticsManager : IDisposable
    {
        const int updateInterval = 1000;

        static Logger logger = LogManager.GetCurrentClassLogger();

        public PerformanceStatisticsManager(CentaurusContext context)
        {
            this.context = context;
            updateTimer = new System.Timers.Timer();
            updateTimer.Interval = updateInterval;
            updateTimer.AutoReset = false;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }

        public abstract event Action<PerformanceStatistics> OnUpdates;

        public void OnBatchSaved(BatchSavedInfo batchInfo)
        {
            lock (syncRoot)
            {
                LastBatchInfos.Add(batchInfo);
                if (LastBatchInfos.Count > 20)
                    LastBatchInfos.RemoveAt(0);
            }
        }

        private List<Apex> RecentApexes = new List<Apex>();
        private List<BatchSavedInfo> LastBatchInfos = new List<BatchSavedInfo>();
        private List<int> LastQuantaQueueLengths = new List<int>();

        private object syncRoot = new { };
        private Timer updateTimer;

        protected readonly CentaurusContext context;

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (syncRoot)
                CollectStatistics();
            updateTimer?.Start();
        }

        protected abstract void CollectStatistics();

        protected int GetQuantaAvgLength()
        {
            LastQuantaQueueLengths.Add(context.QuantumHandler.QuantaQueueLenght);
            if (LastQuantaQueueLengths.Count > 20)
                LastQuantaQueueLengths.RemoveAt(0);
            return (int)Math.Floor(decimal.Divide(LastQuantaQueueLengths.Sum(), LastQuantaQueueLengths.Count));
        }

        protected int GetThrottling()
        {
            return QuantaThrottlingManager.Current.IsThrottlingEnabled ? QuantaThrottlingManager.Current.MaxItemsPerSecond : 0;
        }

        protected int GetItemsPerSecond()
        {
            RecentApexes.Add(new Apex { UpdatedAt = DateTime.UtcNow, CurrentApex = context.QuantumStorage.CurrentApex });
            if (RecentApexes.Count > 20)
                RecentApexes.RemoveAt(0);

            if (RecentApexes.Count < 2)
                return 0;
            var lastItem = RecentApexes.Last();
            var firstItem = RecentApexes.First();
            var timeDiff = (decimal)(lastItem.UpdatedAt - firstItem.UpdatedAt).TotalMilliseconds;
            return (int)(decimal.Divide(lastItem.CurrentApex - firstItem.CurrentApex, timeDiff) * 1000);
        }

        protected List<BatchSavedInfo> GetBatchInfos() => LastBatchInfos;

        public void Dispose()
        {
            lock (syncRoot)
            {
                updateTimer?.Stop();
                updateTimer?.Dispose();
                updateTimer = null;
            }
        }

        protected class Apex
        {
            public long CurrentApex { get; set; }

            public DateTime UpdatedAt { get; set; }
        }
    }

    public class AlphaPerformanceStatisticsManager : PerformanceStatisticsManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaPerformanceStatisticsManager(CentaurusContext context)
            : base(context)
        {
        }

        public override event Action<PerformanceStatistics> OnUpdates;

        private Dictionary<string, AuditorPerformanceStatistics> auditorsStatistics { get; set; } = new Dictionary<string, AuditorPerformanceStatistics>();

        public void AddAuditorStatistics(string accountId, AuditorPerfStatistics message)
        {
            lock (auditorsStatistics)
                auditorsStatistics[accountId] = message.FromModel(accountId);
        }
        private List<AuditorPerformanceStatistics> GetAuditorsStatistics()
        {
            lock (auditorsStatistics)
            {
                var auditorConnections = ((AlphaContext)context).ConnectionManager.GetAuditorConnections();
                foreach (var auditorConnection in auditorConnections)
                {
                    var accountId = auditorConnection?.ClientKPAccountId;
                    if (!auditorsStatistics.TryGetValue(accountId, out var statistics))
                        continue;
                    var auditorApex = auditorConnection?.QuantumWorker?.CurrentApexCursor ?? -1;
                    if (auditorApex >= 0)
                        statistics.Delay = (int)(context.QuantumStorage.CurrentApex - auditorApex);
                }
                return auditorsStatistics.Values.ToList();
            }
        }


        protected override void CollectStatistics()
        {
            try
            {
                var statistics = new AlphaPerformanceStatistics
                {
                    QuantaPerSecond = GetItemsPerSecond(),
                    QuantaQueueLength = GetQuantaAvgLength(),
                    Throttling = GetThrottling(),
                    AuditorStatistics = GetAuditorsStatistics(),
                    BatchInfos = GetBatchInfos(),
                    UpdateDate = DateTime.UtcNow
                };
                OnUpdates?.Invoke(statistics);
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }
    }

    public class AuditorPerformanceStatisticsManager : PerformanceStatisticsManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AuditorPerformanceStatisticsManager(CentaurusContext context)
            : base(context)
        {
        }

        public override event Action<PerformanceStatistics> OnUpdates;

        protected override void CollectStatistics()
        {
            try
            {
                var statistics = new AuditorPerformanceStatistics
                {
                    QuantaPerSecond = GetItemsPerSecond(),
                    QuantaQueueLength = GetQuantaAvgLength(),
                    BatchInfos = GetBatchInfos(),
                    UpdateDate = DateTime.UtcNow
                };
                OnUpdates?.Invoke(statistics);
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }
    }
}
