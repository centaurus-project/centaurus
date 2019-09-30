using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Centaurus.Models
{
    public class OrderRequest : RequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.OrderRequest;

        public int Asset { get; set; }

        public OrderSides Side { get; set; }

        public long Amount { get; set; }

        public double Price { get; set; }

        public TimeInForce TimeInForce { get; set; }
    }
}
