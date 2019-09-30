using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class RequestQuantumSerializer : IXdrSerializer<RequestQuantum>
    {
        public void Deserialize(ref RequestQuantum value, XdrReader reader)
        {
            value.RequestEnvelope = reader.Read<MessageEnvelope>();
        }

        public void Serialize(RequestQuantum value, XdrWriter writer)
        {
            writer.Write(value.RequestEnvelope);
        }
    }
}
