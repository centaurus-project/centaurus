using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationInitEffect : ConstellationEffect
    {
        public override EffectTypes EffectType => EffectTypes.ConstellationInit; 

        [XdrField(0)]
        public long TxCursor { get; set; }
    }
}
