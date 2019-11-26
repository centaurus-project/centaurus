using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class LockLiabilitiesEffect : BaseBalanceEffect
    {
        public override EffectTypes EffectType => EffectTypes.LockLiabilities;
    }
}
