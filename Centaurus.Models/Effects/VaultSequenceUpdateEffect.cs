using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class VaultSequenceUpdateEffect : Effect
    {
        public override EffectTypes EffectType => EffectTypes.VaultSequenceUpdate;

        [XdrField(0)]
        public long Sequence { get; set; }

        [XdrField(1)]
        public long PrevSequence { get; set; }
    }
}
