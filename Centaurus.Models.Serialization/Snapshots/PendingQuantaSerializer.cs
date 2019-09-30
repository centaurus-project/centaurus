using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models.Snapshots
{
    public class PendingQuantaSerializer : IXdrSerializer<PendingQuanta>
    {
        public void Deserialize(ref PendingQuanta value, XdrReader reader)
        {
            value.Quanta = reader.ReadList<MessageEnvelope>();
        }

        public void Serialize(PendingQuanta value, XdrWriter writer)
        {
            writer.Write(value.Quanta);
        }
    }
}
