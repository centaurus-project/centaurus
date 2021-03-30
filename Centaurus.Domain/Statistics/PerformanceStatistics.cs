using System;
using System.Collections.Generic;

namespace Centaurus.Domain
{

    public class PerformanceStatistics
    {
        public int QuantaPerSecond { get; set; }

        public int QuantaQueueLength { get; set; }

        public List<BatchSavedInfo> BatchInfos { get; set; }

        public DateTime UpdateDate { get; set; }
    }

    public class AlphaPerformanceStatistics : PerformanceStatistics
    {
        public int Trottling { get; set; }

        public List<AuditorDelay> AuditorsDelay { get; set; }

        public Dictionary<string, List<PerformanceStatistics>> AuditorStatistics { get; set; }
    }

    public class AuditorDelay
    {
        public string Auditor { get; set; }

        public int Delay { get; set; }
    }

    public class BatchSavedInfo
    {
        public int QuantaCount { get; set; }

        public int EffectsCount { get; set; }

        public int Retries { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public DateTime SavedAt { get; set; }
    }
}
