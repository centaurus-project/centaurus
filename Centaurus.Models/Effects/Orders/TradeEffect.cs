using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class TradeEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.Trade;

        [XdrField(1)]
        public ulong AssetAmount { get; set; }

        [XdrField(2)]
        public ulong QuoteAmount { get; set; }

        [XdrField(3)]
        public bool IsNewOrder { get; set; }
    }
}
