using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class OrderRequest : SequentialRequestMessage
    {
        [XdrField(0)]
        public TimeInForce TimeInForce { get; set; }

        [XdrField(1)]
        public OrderSide Side { get; set; }

        [XdrField(2)]
        public string Asset { get; set; }

        [XdrField(3)]
        public ulong Amount { get; set; }

        [XdrField(4)]
        public double Price { get; set; }
    }
}
