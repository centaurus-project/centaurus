using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderRemovedEffectSerializer : IXdrSerializer<OrderRemovedEffect>
    {
        public void Deserialize(ref OrderRemovedEffect value, XdrReader reader)
        {
            value.OrderId = reader.ReadUInt64();
        }

        public void Serialize(OrderRemovedEffect value, XdrWriter writer)
        {
            writer.Write(value.OrderId);
        }
    }
}
