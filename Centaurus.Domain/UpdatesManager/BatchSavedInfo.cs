using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
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
