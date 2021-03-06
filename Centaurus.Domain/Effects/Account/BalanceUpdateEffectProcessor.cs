﻿using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceUpdateEffectProcesor : BaseAccountEffectProcessor<BalanceUpdateEffect>
    {
        public BalanceUpdateEffectProcesor(BalanceUpdateEffect effect)
            : base(effect)
        {

        }
        public override void CommitEffect()
        {
            MarkAsProcessed();
            var balance = Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            var balance = Account.GetBalance(Effect.Asset);
            balance.UpdateBalance(-Effect.Amount);
        }
    }
}
