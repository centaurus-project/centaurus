using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class TxCursorUpdateEffect : Effect
    {
        public override EffectTypes EffectType => EffectTypes.TxCursorUpdate;

        [XdrField(0)]
        public long Cursor { get; set; }

        [XdrField(1)]
        public long PrevCursor { get; set; }
    }
}
