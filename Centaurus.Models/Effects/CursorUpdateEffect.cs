using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class CursorUpdateEffect : Effect
    {
        public override EffectTypes EffectType => EffectTypes.TxCursorUpdate;

        [XdrField(0)]
        public PaymentProvider Provider { get; set; }

        [XdrField(1)]
        public string Cursor { get; set; }

        [XdrField(2)]
        public string PrevCursor { get; set; }
    }
}
