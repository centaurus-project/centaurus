using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class OrderRemovedEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.OrderRemoved;

        [XdrField(0)]
        public double Price { get; set; }

        [XdrField(1)]
        public ulong Amount { get; set; }

        [XdrField(2)]
        public ulong QuoteAmount { get; set; }

        [XdrField(3)]
        public string Asset { get; set; }

        [XdrField(4)]
        public OrderSide Side { get; set; }
    }
}
