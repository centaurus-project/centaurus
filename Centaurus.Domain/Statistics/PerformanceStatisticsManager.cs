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

        private Dictionary<string, AuditorPerformanceStatistics> auditorsStatistics { get; set; } = new Dictionary<string, AuditorPerformanceStatistics>();

        public void AddAuditorStatistics(string accountId, AuditorPerfStatistics message)
        {
            lock (auditorsStatistics)
                auditorsStatistics[accountId] = message.FromModel(accountId);
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
                        var auditors = GetAuditorsStatistics();
                        statistics = new AlphaPerformanceStatistics
                        {
                            Trottling = throttling,
                            AuditorStatistics = auditors
                        };
                    }
                    else
                        statistics = new AuditorPerformanceStatistics();

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

        private List<AuditorPerformanceStatistics> GetAuditorsStatistics()
        {
            lock (auditorsStatistics)
            {
                var auditorConnections = ConnectionManager.GetAuditorConnections();
                foreach (var auditorConnection in auditorConnections)
                {
                    var accountId = auditorConnection?.ClientKPAccountId;
                    if (!auditorsStatistics.TryGetValue(accountId, out var statistics))
                        continue;
                    var auditorApex = auditorConnection?.QuantumWorker?.CurrentApexCursor ?? -1;
                    if (auditorApex >= 0)
                        statistics.Delay = (int)(Global.QuantumStorage.CurrentApex - auditorApex);
                }
                return auditorsStatistics.Values.ToList();
            }
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
