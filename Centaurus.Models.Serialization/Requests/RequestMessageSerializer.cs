using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class RequestMessageSerializer : IXdrSerializer<RequestMessage>
    {
        public void Deserialize(ref RequestMessage value, XdrReader reader)
        {
            value.Account = reader.Read<RawPubKey>();
            value.Nonce = reader.ReadUInt64();
        }

        public void Serialize(RequestMessage value, XdrWriter writer)
        {
            writer.Write(value.Account);
            writer.Write(value.Nonce);
        }
    }
}
