using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class WithdrawalCreateEffect: WithdrawalBaseEffect
    {
        public override EffectTypes EffectType => EffectTypes.WithdrawalCreate;
    }
}
