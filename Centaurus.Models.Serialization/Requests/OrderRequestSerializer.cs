using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Centaurus.Models
{
    public class OrderRequestSerializer: IXdrSerializer<OrderRequest>
    {
        public void Deserialize(ref OrderRequest value, XdrReader reader)
        {
            value.Asset = reader.ReadInt32();
            value.Side = reader.ReadEnum<OrderSides>();
            value.Amount = reader.ReadInt64();
            value.Price = reader.ReadDouble();
            value.TimeInForce = reader.ReadEnum<TimeInForce>();
        }

        public void Serialize(OrderRequest value, XdrWriter writer)
        {
            writer.Write(value.Asset);
            writer.Write(value.Side);
            writer.Write(value.Amount);
            writer.Write(value.Price);
            writer.Write(value.TimeInForce);
        }
    }
}
