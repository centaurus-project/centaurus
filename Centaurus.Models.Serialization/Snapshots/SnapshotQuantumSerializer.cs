using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class SnapshotQuantumSerializer : IXdrSerializer<SnapshotQuantum>
    {
        public void Deserialize(ref SnapshotQuantum value, XdrReader reader)
        {
            value.Hash = reader.ReadVariable();
        }

        public void Serialize(SnapshotQuantum value, XdrWriter writer)
        {
            writer.Write(value.Hash);
        }
    }
}
