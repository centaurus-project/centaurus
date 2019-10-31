using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderRemovedEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.OrderRemoved;

        [XdrField(0)]
        public ulong OrderId { get; set; }
    }
}
