using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class TradeEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.Trade;

        public ulong OrderId { get; set; }

        public OrderSides OrderSide { get; set; }

        public int Asset { get; set; }

        public long AssetAmount { get; set; }

        public long XlmAmount { get; set; }

        public double Price { get; set; }
    }
}
