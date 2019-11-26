using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class LedgerUpdateEffect : Effect
    {
        public override EffectTypes EffectType => EffectTypes.LedgerUpdate;

        [XdrField(0)]
        public long Ledger { get; set; }

        [XdrField(1)]
        public long PrevLedger { get; set; }
    }
}
