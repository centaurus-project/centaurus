using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class TradeEffectSerializer : IXdrSerializer<TradeEffect>
    {
        public void Deserialize(ref TradeEffect value, XdrReader reader)
        {
            value.OrderId = reader.ReadUInt64();
            value.OrderSide = reader.ReadEnum<OrderSides>();
            value.Asset = reader.ReadInt32();
            value.AssetAmount = reader.ReadInt64();
            value.XlmAmount = reader.ReadInt64();
            value.Price = reader.ReadDouble();
        }

        public void Serialize(TradeEffect value, XdrWriter writer)
        {
            writer.Write(value.OrderId);
            writer.Write(value.OrderSide);
            writer.Write(value.Asset);
            writer.Write(value.AssetAmount);
            writer.Write(value.XlmAmount);
            writer.Write(value.Price);
        }
    }
}
