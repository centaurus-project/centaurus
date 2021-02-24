using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class WithdrawalRemoveEffect: WithdrawalBaseEffect
    {
        public override EffectTypes EffectType => EffectTypes.WithdrawalRemove;

        [XdrField(0)]
        public bool IsSuccessful { get; set; }
    }
}
