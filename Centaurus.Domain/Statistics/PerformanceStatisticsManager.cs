using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    public class PerformanceStatisticsManager : ContextualBase, IDisposable
    {
        const int updateInterval = 1000;

        static Logger logger = LogManager.GetCurrentClassLogger();

        public PerformanceStatisticsManager(ExecutionContext context)
            : base(context)
        {
            updateTimer = new System.Timers.Timer();
            updateTimer.Interval = updateInterval;
            updateTimer.AutoReset = false;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }

        public void OnBatchSaved(BatchSavedInfo batchInfo)
        {
            lock (syncRoot)
            {
                LastBatchInfos.Add(batchInfo);
                if (LastBatchInfos.Count > 20)
                    LastBatchInfos.RemoveAt(0);
            }
        }

        public void AddAuditorStatistics(string accountId, AuditorPerfStatistics message)
        {
            lock (auditorsStatistics)
                auditorsStatistics[accountId] = message.FromModel(accountId);
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

        List<Apex> RecentApexes = new List<Apex>();
        List<BatchSavedInfo> LastBatchInfos = new List<BatchSavedInfo>();
        List<int> LastQuantaQueueLengths = new List<int>();

        object syncRoot = new { };
        Timer updateTimer;

        void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (syncRoot)
                CollectStatistics();
            updateTimer?.Start();
        }

        Dictionary<string, AuditorPerformanceStatistics> auditorsStatistics { get; set; } = new Dictionary<string, AuditorPerformanceStatistics>();
        List<AuditorPerformanceStatistics> GetAuditorsStatistics()
        {
            lock (auditorsStatistics)
            {
                var auditorConnections = Context.ConnectionManager.GetAuditorConnections();
                foreach (var auditorConnection in auditorConnections)
                {
                    var accountId = auditorConnection?.PubKeyAddress;
                    if (!auditorsStatistics.TryGetValue(accountId, out var statistics))
                        continue;
                    var auditorApex = (ulong)(auditorConnection?.QuantumWorker?.CurrentApexCursor ?? 0);
                    if (auditorApex >= 0)
                        statistics.Delay = (int)(Context.QuantumStorage.CurrentApex - auditorApex);
                }
                return auditorsStatistics.Values.ToList();
            }
        }

        void CollectStatistics()
        {
            try
            {
                var statistics = default(PerformanceStatistics);
                if (Context.IsAlpha)
                {
                    if (!Context.SubscriptionsManager.TryGetSubscription(PerformanceStatisticsSubscription.SubscriptionName, out var subscription))
                        return;
                    statistics = new AlphaPerformanceStatistics
                    {
                        QuantaPerSecond = GetItemsPerSecond(),
                        QuantaQueueLength = GetQuantaAvgLength(),
                        Throttling = GetThrottling(),
                        AuditorStatistics = GetAuditorsStatistics(),
                        BatchInfos = GetBatchInfos(),
                        UpdateDate = DateTime.UtcNow
                    };
                    Context.InfoConnectionManager.SendSubscriptionUpdate(subscription, PerformanceStatisticsUpdate.Generate((AlphaPerformanceStatistics)statistics, PerformanceStatisticsSubscription.SubscriptionName));
                }
                else
                {
                    statistics = new AuditorPerformanceStatistics
                    {
                        QuantaPerSecond = GetItemsPerSecond(),
                        QuantaQueueLength = GetQuantaAvgLength(),
                        BatchInfos = GetBatchInfos(),
                        UpdateDate = DateTime.UtcNow
                    };
                    Context.OutgoingMessageStorage.EnqueueMessage(statistics.ToModel());
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }

        int GetQuantaAvgLength()
        {
            LastQuantaQueueLengths.Add(Context.QuantumHandler.QuantaQueueLenght);
            if (LastQuantaQueueLengths.Count > 20)
                LastQuantaQueueLengths.RemoveAt(0);
            return (int)Math.Floor(decimal.Divide(LastQuantaQueueLengths.Sum(), LastQuantaQueueLengths.Count));
        }

        int GetThrottling()
        {
            return QuantaThrottlingManager.Current.IsThrottlingEnabled ? QuantaThrottlingManager.Current.MaxItemsPerSecond : 0;
        }

        int GetItemsPerSecond()
        {
            RecentApexes.Add(new Apex { UpdatedAt = DateTime.UtcNow, CurrentApex = Context.QuantumStorage.CurrentApex });
            if (RecentApexes.Count > 20)
                RecentApexes.RemoveAt(0);

            if (RecentApexes.Count < 2)
                return 0;
            var lastItem = RecentApexes.Last();
            var firstItem = RecentApexes.First();
            var timeDiff = (decimal)(lastItem.UpdatedAt - firstItem.UpdatedAt).TotalMilliseconds;
            return (int)(decimal.Divide(lastItem.CurrentApex - firstItem.CurrentApex, timeDiff) * 1000);
        }

        List<BatchSavedInfo> GetBatchInfos() => LastBatchInfos;

        class Apex
        {
            public ulong CurrentApex { get; set; }

            public DateTime UpdatedAt { get; set; }
        }
    }
}
