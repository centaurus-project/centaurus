using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AuditorPerfStatistics : Message
    {
        [XdrField(0)]
        public int QuantaPerSecond { get; set; }

        [XdrField(1)]
        public int QuantaQueueLength { get; set; }

        [XdrField(2)]
        public List<BatchSavedInfo> BatchInfos { get; set; }

        [XdrField(3)]
        public long UpdateDate { get; set; }

        [XdrField(4)]
        public State State { get; set; }

        public override MessageTypes MessageType => MessageTypes.AuditorPerfStatistics;
    }

    [XdrContract]
    public class BatchSavedInfo
    {
        [XdrField(0)]
        public int QuantaCount { get; set; }

        [XdrField(1)]
        public int EffectsCount { get; set; }

        [XdrField(2)]
        public long ElapsedMilliseconds { get; set; }

        [XdrField(3)]
        public long SavedAt { get; set; }
    }
}
