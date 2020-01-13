using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderRemovedEffect: BaseOrderEffect
    {
        public override EffectTypes EffectType => EffectTypes.OrderRemoved;
    }
}
