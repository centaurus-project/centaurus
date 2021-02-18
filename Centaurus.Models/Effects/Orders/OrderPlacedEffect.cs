using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class OrderPlacedEffect: BaseOrderEffect
    {
        public override EffectTypes EffectType => EffectTypes.OrderPlaced;

        [XdrField(0)]
        public double Price { get; set; }

        [XdrField(1)]
        public OrderSide OrderSide { get; set; }

        [XdrField(2)]
        public long Amount { get; set; }

        [XdrField(3)]
        public long QuoteAmount { get; set; }

        [XdrField(4)]
        public long Asset { get; set; }
    }
}
