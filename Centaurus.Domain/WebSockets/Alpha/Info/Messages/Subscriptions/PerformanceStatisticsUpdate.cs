using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class PerformanceStatisticsUpdate : SubscriptionUpdateBase
    {
        public int QuantaPerSecond { get; set; }

        public int Throttling { get; set; }

        public int QuantaQueueLength { get; private set; }

        public List<BatchSavedInfo> BatchSavedInfos { get; set; }

        public List<AuditorDelay> AuditorsDelay { get; private set; }

        public Dictionary<string, List<PerformanceStatistics>> AuditorStatistics { get; private set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            var batches = BatchSavedInfos.Where(b => b.SavedAt > lastUpdateDate).ToList();
            var statistics = new Dictionary<string, List<PerformanceStatistics>>(
                AuditorStatistics
                    .Select(@as => KeyValuePair.Create(@as.Key, @as.Value.Where(s => s.UpdateDate > lastUpdateDate)
                    .ToList()))
            );
            //all data are new for the client
            if (batches.Count == BatchSavedInfos.Count 
                && statistics.Values.Sum(v => v.Count) == AuditorStatistics.Values.Sum(v => v.Count))
                return this;

            return new PerformanceStatisticsUpdate
            {
                QuantaPerSecond = QuantaPerSecond,
                Throttling = Throttling,
                BatchSavedInfos = batches, //send only new data
                UpdateDate = UpdateDate,
                QuantaQueueLength = QuantaQueueLength,
                AuditorsDelay = AuditorsDelay,
                AuditorStatistics = statistics,
                ChannelName = ChannelName
            };
        }

        public static PerformanceStatisticsUpdate Generate(AlphaPerformanceStatistics update, string channelName)
        {
            return new PerformanceStatisticsUpdate
            {
                QuantaPerSecond = update.QuantaPerSecond,
                Throttling = update.Trottling,
                BatchSavedInfos = update.BatchInfos,
                ChannelName = channelName,
                QuantaQueueLength = update.QuantaQueueLength,
                AuditorsDelay = update.AuditorsDelay,
                AuditorStatistics = update.AuditorStatistics,
                UpdateDate = DateTime.UtcNow
            };
        }
    }
}
