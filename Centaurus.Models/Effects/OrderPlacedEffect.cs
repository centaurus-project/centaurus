using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderPlacedEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.OrderPlaced;

        public ulong OrderId { get; set; }
        
        public OrderSides OrderSide { get; set; }

        public int Asset { get; set; }

        public long Amount { get; set; }

        public double Price { get; set; }
    }
}
