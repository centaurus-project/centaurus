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

        public List<PendingUpdatesManager.BatchSavedInfo> BatchSavedInfos { get; set; }

        public Dictionary<string, int> AuditorsDelay { get; private set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            //all data are new for the client
            if (BatchSavedInfos.All(b => b.SavedAt > lastUpdateDate))
                return this;

            return new PerformanceStatisticsUpdate
            {
                QuantaPerSecond = QuantaPerSecond,
                Throttling = Throttling,
                BatchSavedInfos = BatchSavedInfos.Where(b => b.SavedAt > lastUpdateDate).ToList(), //send only new data
                UpdateDate = UpdateDate,
                QuantaQueueLength = QuantaQueueLength,
                AuditorsDelay = AuditorsDelay,
                ChannelName = ChannelName
            };
        }

        public static PerformanceStatisticsUpdate Generate(PerformanceStatisticsManager.PerformanceStatisticsManagerUpdate update, string channelName)
        {
            return new PerformanceStatisticsUpdate
            {
                QuantaPerSecond = update.QuantaPerSecond,
                Throttling = update.Trottling,
                BatchSavedInfos = update.BatchInfos,
                ChannelName = channelName,
                QuantaQueueLength = update.QuantaQueueLength,
                AuditorsDelay = update.AuditorsDelay,
                UpdateDate = DateTime.UtcNow
            };
        }
    }
}
