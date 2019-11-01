using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Centaurus.Models
{
    public class OrderRequest : RequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.OrderRequest;
        
        [XdrField(0)]
        public TimeInForce TimeInForce { get; set; }

        [XdrField(1)]
        public OrderSides Side { get; set; }

        [XdrField(2)]
        public int Asset { get; set; }

        [XdrField(3)]
        public long Amount { get; set; }

        [XdrField(4)]
        public double Price { get; set; }
    }
}
