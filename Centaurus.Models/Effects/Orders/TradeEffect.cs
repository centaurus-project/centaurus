﻿using System;
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
        public long AssetAmount { get; set; }

        [XdrField(2)]
        public long QuoteAmount { get; set; }

        [XdrField(3)]
        public bool IsNewOrder { get; set; }
    }
}
