using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class HeartbeatSerializer : IXdrSerializer<Heartbeat>
    {
        public void Deserialize(ref Heartbeat value, XdrReader reader) { }

        public void Serialize(Heartbeat value, XdrWriter writer) { }
    }
}
