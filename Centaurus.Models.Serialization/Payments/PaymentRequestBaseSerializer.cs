using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class PaymentRequestBaseSerializer : IXdrSerializer<PaymentRequestBase>
    {
        public void Deserialize(ref PaymentRequestBase value, XdrReader reader)
        {
            value.Asset = reader.ReadInt32();
            value.Amount = reader.ReadInt64();
            value.Destination = reader.Read<RawPubKey>();
            value.Memo = reader.ReadString();
            value.TransactionHash = reader.ReadVariable();
            //TODO: do we need to serialize Transaction?
        }

        public void Serialize(PaymentRequestBase value, XdrWriter writer)
        {
            writer.Write(value.Asset);
            writer.Write(value.Amount);
            writer.Write(value.Destination);
            writer.Write(value.Memo);
            writer.Write(value.TransactionHash);
        }
    }
}
