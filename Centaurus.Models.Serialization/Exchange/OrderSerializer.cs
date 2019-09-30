using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderSerializer : IXdrSerializer<Order>
    {
        public void Deserialize(ref Order value, XdrReader reader)
        {
            value.OrderId = reader.ReadUInt64();
            value.Price = reader.ReadDouble();
            value.Amount = reader.ReadInt64();
            value.Pubkey = reader.Read<RawPubKey>();
        }

        public void Serialize(Order value, XdrWriter writer)
        {
            writer.Write(value.OrderId);
            writer.Write(value.Price);
            writer.Write(value.Amount);
            writer.Write(value.Pubkey.Data);
        }
    }
}
