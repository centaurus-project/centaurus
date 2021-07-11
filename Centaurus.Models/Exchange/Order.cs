using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class Order
    {
        [XdrField(0)]
        public string Asset { get; set; }

        [XdrField(1)]
        public OrderSide Side { get; set; }

        [XdrField(2)]
        public double Price { get; set; }

        [XdrField(3)]
        public ulong Amount { get; set; }

        [XdrField(4)]
        public ulong QuoteAmount { get; set; }

        [XdrField(5)]
        public ulong OrderId { get; set; }
    }
}
