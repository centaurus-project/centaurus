using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models.Auditor
{
    public class AuditorStateSerializer : IXdrSerializer<AuditorState>
    {
        public void Deserialize(ref AuditorState value, XdrReader reader)
        {
            value.State = reader.ReadEnum<ApplicationState>();
            value.LastSnapshot = reader.ReadOptional<Snapshot>();
            value.PendingQuantums = reader.ReadList<MessageEnvelope>();
        }

        public void Serialize(AuditorState value, XdrWriter writer)
        {
            writer.Write(value.State);
            writer.WriteOptional(value.LastSnapshot);
            writer.Write(value.PendingQuantums);
        }
    }
}
