using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class WithdrawalCreateEffect: Effect
    {
        public override EffectTypes EffectType => EffectTypes.WithdrawalCreate;

        [XdrField(0)]
        public Withdrawal Withdrawal { get; set; }
    }
}
