using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class UnlockLiabilitiesEffect : BaseBalanceEffect
    {
        public override EffectTypes EffectType => EffectTypes.UnlockLiabilities;
    }
}
