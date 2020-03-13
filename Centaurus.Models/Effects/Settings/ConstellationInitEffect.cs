using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationInitEffect : ConstellationEffect
    {
        [XdrField(0)]
        public long Ledger { get; set; }

        [XdrField(1)]
        public long VaultSequence { get; set; }

        public override EffectTypes EffectType => EffectTypes.ConstellationInit; 
    }
}
