using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    public class PerformanceStatisticsManager : IDisposable
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

        private Dictionary<string, List<PerformanceStatistics>> auditorsStatistics { get; set; } = new Dictionary<string, List<PerformanceStatistics>>();

        public void AddAuditorStatistics(string accountId, AuditorPerfStatistics message)
        {
            var statistics = default(List<PerformanceStatistics>);
            lock (auditorsStatistics)
                if (!auditorsStatistics.TryGetValue(accountId, out statistics))
                {
                    statistics = new List<PerformanceStatistics>();
                    auditorsStatistics.Add(accountId, statistics);
                }

            lock (statistics)
            {
                statistics.Add(message.FromModel());
                if (statistics.Count > 20)
                    statistics.RemoveAt(0);
            }
        }

        public event Action<PerformanceStatistics> OnUpdates;

        public void OnBatchSaved(BatchSavedInfo batchInfo)
        {
            lock (syncRoot)
            {
                LastBatchInfos.Add(batchInfo);
                if (LastBatchInfos.Count > 20)
                    LastBatchInfos.RemoveAt(0);
            }
        }

        private List<Apex> LastApexes = new List<Apex>();
        private List<BatchSavedInfo> LastBatchInfos = new List<BatchSavedInfo>();
        private List<int> LastQuantaQueueLengths = new List<int>();

        private object syncRoot = new { };
        private Timer updateTimer;

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (syncRoot)
            {
                try
                {
                    var quantaPerSecond = GetItemsPerSecond();
                    var quantaAvgLength = GetQuantaAvgLength();
                    var statistics = default(PerformanceStatistics);
                    if (Global.IsAlpha)
                    {
                        var throttling = GetThrottling();
                        var auditorsDelay = GetAuditorsDelay();
                        var auditors = GetAuditorsStatistics();
                        statistics = new AlphaPerformanceStatistics
                        {
                            AuditorsDelay = auditorsDelay,
                            Trottling = throttling,
                            AuditorStatistics = auditors
                        };
                    }
                    else
                        statistics = new PerformanceStatistics();

                    statistics.QuantaPerSecond = quantaPerSecond;
                    statistics.QuantaQueueLength = quantaAvgLength;
                    statistics.BatchInfos = LastBatchInfos;
                    statistics.UpdateDate = DateTime.UtcNow;

                    OnUpdates?.Invoke(statistics);
                    updateTimer?.Start();
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                }
            }
        }

        private Dictionary<string, List<PerformanceStatistics>> GetAuditorsStatistics()
        {
            lock (auditorsStatistics)
                return auditorsStatistics.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()); //copy dictionary to avoid collection modification exception
        }

        private List<AuditorDelay> GetAuditorsDelay()
        {
            var auditors = ConnectionManager.GetAuditorConnections();
            var delays = new List<AuditorDelay>();
            foreach (var auditor in auditors)
            {
                var client = auditor?.ClientKPAccountId;
                var auditorApex = auditor?.QuantumWorker?.CurrentApexCursor ?? -1;
                if (auditorApex >= 0)
                    delays.Add(new AuditorDelay
                    {
                        Auditor = client,
                        Delay = (int)(auditorApex - Global.QuantumStorage.CurrentApex)
                    });
            }
            return delays;
        }

        private int GetQuantaAvgLength()
        {
            LastQuantaQueueLengths.Add(Global.QuantumHandler.QuantaQueueLenght);
            if (LastQuantaQueueLengths.Count > 20)
                LastQuantaQueueLengths.RemoveAt(0);
            return (int)Math.Floor(decimal.Divide(LastQuantaQueueLengths.Sum(), LastQuantaQueueLengths.Count));
        }

        private int GetThrottling()
        {
            return QuantaThrottlingManager.Current.IsThrottlingEnabled ? QuantaThrottlingManager.Current.MaxItemsPerSecond : 0;
        }

        private int GetItemsPerSecond()
        {
            LastApexes.Add(new Apex { UpdatedAt = DateTime.UtcNow, CurrentApex = Global.QuantumStorage.CurrentApex });
            if (LastApexes.Count > 20)
                LastApexes.RemoveAt(0);

            if (LastApexes.Count < 2)
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

        class Apex
        {
            public long CurrentApex { get; set; }

            public DateTime UpdatedAt { get; set; }
        }
    }
}
