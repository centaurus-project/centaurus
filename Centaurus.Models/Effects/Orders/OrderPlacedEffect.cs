using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class OrderPlacedEffect: BaseOrderEffect
    {
        public override EffectTypes EffectType => EffectTypes.OrderPlaced;

        [XdrField(0)]
        public OrderSides OrderSide { get; set; }

        [XdrField(1)]
        public long Amount { get; set; }

        [XdrField(2)]
        public long Asset { get; set; }
    }
}
