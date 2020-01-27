using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class OrderRemovedEffect: BaseOrderEffect
    {
        public override EffectTypes EffectType => EffectTypes.OrderRemoved;
    }
}
