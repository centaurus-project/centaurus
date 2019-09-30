using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class MessageEnvelopeSerializer : IXdrSerializer<MessageEnvelope>
    {
        public void Deserialize(ref MessageEnvelope value, XdrReader reader)
        {
            value.Message = reader.Read<Message>();
            value.Signatures = reader.ReadList<Ed25519Signature>();
        }

        public void Serialize(MessageEnvelope value, XdrWriter writer)
        {
            writer.Write(value.Message);
            writer.Write(value.Signatures);
        }
    }
}
