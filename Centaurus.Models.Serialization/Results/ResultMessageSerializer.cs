using stellar_dotnet_sdk.xdr;
using System;

namespace Centaurus.Models
{
    public class ResultMessageSerializer : IXdrSerializer<ResultMessage>
    {
        public void Deserialize(ref ResultMessage value, XdrReader reader)
        {
            value.Status = reader.ReadEnum<ResultStatusCodes>();
            value.OriginalMessage = reader.Read<MessageEnvelope>();
        }

        public void Serialize(ResultMessage value, XdrWriter writer)
        {
            writer.Write(value.Status);
            writer.Write(value.OriginalMessage);
        }
    }
}
