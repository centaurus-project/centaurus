using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain.Quanta.Sync
{
    public class SyncPortion
    {
        public SyncPortion(byte[] data, ulong lastDataApex)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            LastDataApex = lastDataApex;
        }

        public byte[] Data { get; }

        public ulong LastDataApex { get; }
    }
}
