using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderRemovedEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.OrderRemoved;

        public ulong OrderId { get; set; }
    }
}
