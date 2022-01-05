using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class PerformanceStatisticsUpdate : SubscriptionUpdateBase
    {
        public List<PerformanceStatistics> AuditorStatistics { get; private set; }

        public override SubscriptionUpdateBase GetUpdateForDate(DateTime lastUpdateDate)
        {
            if (UpdateDate < lastUpdateDate)
                return null;
            return this;
        }

        public static PerformanceStatisticsUpdate Generate(List<PerformanceStatistics> update, string channelName)
        {
            return new PerformanceStatisticsUpdate
            {
                AuditorStatistics = update,
                ChannelName = channelName,
                UpdateDate = DateTime.UtcNow
            };
        }
    }
}
