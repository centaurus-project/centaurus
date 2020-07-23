using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class NonceUpdateEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.NonceUpdate;
        
        [XdrField(0)]
        public long Nonce { get; set; }

        [XdrField(1)]
        public long PrevNonce { get; set; }
    }
}
