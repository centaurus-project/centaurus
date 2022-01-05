using System;
using System.Collections.Generic;

namespace Centaurus.Domain
{

    public class PerformanceStatistics
    {
        public string PublicKey { get; set; }

        public ulong Apex { get; set; }

        public ulong PersistedApex { get; set; }

        public int QuantaPerSecond { get; set; }

        public int QuantaQueueLength { get; set; }

        public int Throttling { get; set; }

        public int State { get; set; }

        public DateTime UpdateDate { get; set; }

        public long ApexDiff { get; set; }

        public PerformanceStatistics Clone()
        {
            return (PerformanceStatistics)MemberwiseClone();
        }
    }
}
