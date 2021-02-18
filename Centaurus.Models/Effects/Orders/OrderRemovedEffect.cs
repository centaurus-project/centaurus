using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class OrderRemovedEffect: BaseOrderEffect
    {
        public override EffectTypes EffectType => EffectTypes.OrderRemoved;

        [XdrField(0)]
        public double Price { get; set; }

        [XdrField(1)]
        public long Amount { get; set; }

        [XdrField(2)]
        public long QuoteAmount { get; set; }

        [XdrField(3)]
        public long Asset { get; set; }
    }
}
