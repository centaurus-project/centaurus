using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationUpdateEffect : ConstellationEffect
    {
        public override EffectTypes EffectType => EffectTypes.ConstellationUpdate;
    }
}
