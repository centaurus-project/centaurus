using System;
using System.Collections.Generic;

namespace Centaurus.Domain
{

    public abstract class PerformanceStatistics
    {
        public int QuantaPerSecond { get; set; }

        public int QuantaQueueLength { get; set; }

        public List<BatchSavedInfo> BatchInfos { get; set; }

        public int Throttling { get; set; }

        public int State { get; set; }

        public DateTime UpdateDate { get; set; }
    }

    public class AlphaPerformanceStatistics : PerformanceStatistics
    {
        public List<AuditorPerformanceStatistics> AuditorStatistics { get; set; }

        public AlphaPerformanceStatistics Clone()
        {
            return (AlphaPerformanceStatistics)MemberwiseClone();
        }
    }

    public class AuditorPerformanceStatistics : PerformanceStatistics
    {
        public string Auditor { get; set; }

        public int Delay { get; set; } = -1;

        public AuditorPerformanceStatistics Clone()
        {
            return (AuditorPerformanceStatistics)MemberwiseClone();
        }
    }

    public class BatchSavedInfo
    {
        public int QuantaCount { get; set; }

        public int EffectsCount { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public DateTime SavedAt { get; set; }

        public ulong FromApex { get; internal set; }

        public ulong ToApex { get; internal set; }
    }
}
