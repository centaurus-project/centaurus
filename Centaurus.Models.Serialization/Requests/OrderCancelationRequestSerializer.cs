using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models
{
    public class OrderCancelationRequestSerializer: IXdrSerializer<OrderCancelationRequest>
    {
        public void Deserialize(ref OrderCancelationRequest value, XdrReader reader)
        {
            value.OrderId = reader.ReadUInt64();
        }

        public void Serialize(OrderCancelationRequest value, XdrWriter writer)
        {
            writer.Write(value.OrderId);
        }
    }
}
