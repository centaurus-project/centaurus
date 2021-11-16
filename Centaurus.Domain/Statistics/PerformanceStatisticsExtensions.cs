using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class PerformanceStatisticsExtensions
    {
        public static AuditorPerformanceStatistics FromModel(this AuditorPerfStatistics statistics, string auditor)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));
            return new AuditorPerformanceStatistics
            {
                Auditor = auditor,
                QuantaPerSecond = statistics.QuantaPerSecond,
                QuantaQueueLength = statistics.QuantaQueueLength,
                BatchInfos = statistics.BatchInfos.Select(b => b.FromModel()).ToList(),
                UpdateDate = new DateTime(statistics.UpdateDate, DateTimeKind.Utc),
                State = (int)statistics.State
            };
        }

        public static AuditorPerfStatistics ToModel(this PerformanceStatistics statistics)
        {
            return new AuditorPerfStatistics
            {
                QuantaPerSecond = statistics.QuantaPerSecond,
                QuantaQueueLength = statistics.QuantaQueueLength,
                BatchInfos = statistics.BatchInfos.Select(b => b.ToBatchSavedInfoModel()).ToList(),
                UpdateDate = statistics.UpdateDate.Ticks,
                State = (State)statistics.State
            };
        }
    }
}
