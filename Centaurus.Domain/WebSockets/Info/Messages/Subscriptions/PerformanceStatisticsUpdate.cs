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

        public List<AuditorPerformanceStatistics> AuditorStatistics { get; private set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            var batches = BatchSavedInfos.Where(b => b.SavedAt > lastUpdateDate).ToList();

            var statistics = new List<AuditorPerformanceStatistics>();
            foreach (var auditorStatistic in AuditorStatistics)
            {
                if (auditorStatistic.UpdateDate < lastUpdateDate)
                    continue;
                var auditorBatches = auditorStatistic.BatchInfos.Where(b => b.SavedAt > lastUpdateDate).ToList();
                if (auditorBatches.Count == auditorStatistic.BatchInfos.Count)
                    statistics.Add(auditorStatistic);
                else
                {
                    var statistic = auditorStatistic.Clone();
                    statistic.BatchInfos = auditorBatches;
                    statistics.Add(statistic);
                }
            }

            //all data are new for the client
            if (batches.Count == BatchSavedInfos.Count
                && statistics.Sum(s => s.BatchInfos.Count) == AuditorStatistics.Sum(s => s.BatchInfos.Count))
                return this;

            return new PerformanceStatisticsUpdate
            {
                QuantaPerSecond = QuantaPerSecond,
                Throttling = Throttling,
                BatchSavedInfos = batches, //send only new data
                QuantaQueueLength = QuantaQueueLength,
                AuditorStatistics = statistics,
                ChannelName = ChannelName,
                UpdateDate = UpdateDate
            };
        }

        public static PerformanceStatisticsUpdate Generate(AlphaPerformanceStatistics update, string channelName)
        {
            return new PerformanceStatisticsUpdate
            {
                QuantaPerSecond = update.QuantaPerSecond,
                Throttling = update.Throttling,
                BatchSavedInfos = update.BatchInfos,
                ChannelName = channelName,
                QuantaQueueLength = update.QuantaQueueLength,
                AuditorStatistics = update.AuditorStatistics,
                UpdateDate = DateTime.UtcNow
            };
        }
    }
}
