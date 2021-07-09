using Centaurus.Xdr;
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
        public string Asset { get; set; }
    }
}
