using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class OrderPlacedEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.OrderPlaced;

        [XdrField(0)]
        public ulong OrderId { get; set; }

        [XdrField(1)]
        public OrderSides OrderSide { get; set; }

        [XdrField(2)]
        public int Asset { get; set; }

        [XdrField(3)]
        public long Amount { get; set; }

        [XdrField(4)]
        public double Price { get; set; }
    }
}
