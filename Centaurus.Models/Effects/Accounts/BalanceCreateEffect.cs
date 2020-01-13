using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class BalanceCreateEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.BalanceCreate;

        [XdrField(0)]
        public int Asset { get; set; }
    }
}
