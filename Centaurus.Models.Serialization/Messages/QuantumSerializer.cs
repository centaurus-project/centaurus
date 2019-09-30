using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class QuantumSerializer : IXdrSerializer<Quantum>
    {
        public void Deserialize(ref Quantum value, XdrReader reader)
        {
            value.Apex = reader.ReadUInt64();
            value.Timestamp = reader.ReadUInt64();
        }

        public void Serialize(Quantum value, XdrWriter writer)
        {
            writer.Write(value.Apex);
            writer.Write(value.Timestamp);
        }
    }
}