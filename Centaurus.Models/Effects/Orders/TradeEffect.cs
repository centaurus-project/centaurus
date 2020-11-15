using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class TradeEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.Trade;

        [XdrField(0)]
        public ulong OrderId { get; set; }

        [XdrField(1)]
        public OrderSide OrderSide { get; set; }

        [XdrField(2)]
        public int Asset { get; set; }

        [XdrField(3)]
        public long AssetAmount { get; set; }

        [XdrField(4)]
        public long XlmAmount { get; set; }

        [XdrField(5)]
        public double Price { get; set; }
    }
}
