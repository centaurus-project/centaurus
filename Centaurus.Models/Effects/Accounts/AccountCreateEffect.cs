using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class AccountCreateEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.AccountCreate;

        [XdrField(0)]
        public RawPubKey Pubkey { get; set; }
    }
}
