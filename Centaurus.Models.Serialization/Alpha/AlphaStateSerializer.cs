using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AlphaStateSerializer : IXdrSerializer<AlphaState>
    {
        public void Deserialize(ref AlphaState value, XdrReader reader)
        {
            value.State = reader.ReadEnum<ApplicationState>();
            value.LastSnapshot = reader.Read<Snapshot>();
        }

        public void Serialize(AlphaState value, XdrWriter writer)
        {
            writer.Write(value.State);
            writer.Write(value.LastSnapshot);
        }
    }
}
