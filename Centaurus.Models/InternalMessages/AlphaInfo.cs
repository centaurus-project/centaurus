using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AlphaState : Message
    {
        public override MessageTypes MessageType => MessageTypes.AlphaState;

        public ApplicationState State { get; set; }

        public Snapshot LastSnapshot { get; set; }

        public void Deserialize(ref AlphaState value, XdrReader reader)
        {
            value.State = reader.ReadEnum<ApplicationState>();
            value.LastSnapshot = reader.ReadObject<Snapshot>();
        }

        public void Serialize(AlphaState value, XdrWriter writer)
        {
            writer.WriteEnum(value.State);
            writer.WriteObject(value.LastSnapshot);
        }
    }
}
