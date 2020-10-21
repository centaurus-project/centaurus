using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class WithdrawalRemoveEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.WithdrawalRemove;
    }
}
