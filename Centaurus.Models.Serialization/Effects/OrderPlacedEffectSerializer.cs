using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderPlacedEffectSerializer : IXdrSerializer<OrderPlacedEffect>
    {
        public void Deserialize(ref OrderPlacedEffect value, XdrReader reader)
        {
            value.OrderId = reader.ReadUInt64();
            value.OrderSide = reader.ReadEnum<OrderSides>();
            value.Asset = reader.ReadInt32();
            value.Amount = reader.ReadInt64();
            value.Price = reader.ReadDouble();
        }

        public void Serialize(OrderPlacedEffect value, XdrWriter writer)
        {
            writer.Write(value.OrderId);
            writer.Write(value.OrderSide);
            writer.Write(value.Asset);
            writer.Write(value.Amount);
            writer.Write(value.Price);
        }
    }
}
