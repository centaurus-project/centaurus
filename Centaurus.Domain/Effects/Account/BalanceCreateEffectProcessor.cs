using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceCreateEffectProcessor : BaseAccountEffectProcessor<BalanceCreateEffect>
    {
        public BalanceCreateEffectProcessor(BalanceCreateEffect effect)
            : base(effect)
        {

        }
        public override void CommitEffect()
        {
            MarkAsProcessed();
            Account.CreateBalance(Effect.Asset);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            Account.Balances.Remove(Account.GetBalance(Effect.Asset));
        }
    }
}
