using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class OrderPlacedEffect: AccountEffect
    {
        [XdrField(0)]
        public double Price { get; set; }

        [XdrField(1)]
        public OrderSide Side { get; set; }

        [XdrField(2)]
        public ulong Amount { get; set; }

        [XdrField(3)]
        public ulong QuoteAmount { get; set; }

        [XdrField(4)]
        public string Asset { get; set; }

        public ulong OrderId => Apex;
    }
}
