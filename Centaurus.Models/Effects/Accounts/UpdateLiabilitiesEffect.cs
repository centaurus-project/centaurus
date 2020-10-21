using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class UpdateLiabilitiesEffect : BaseBalanceEffect
    {
        public override EffectTypes EffectType => EffectTypes.UpdateLiabilities;
    }
}
