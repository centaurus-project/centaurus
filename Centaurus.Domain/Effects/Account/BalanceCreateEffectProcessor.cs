using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceCreateEffectProcessor : BaseAccountEffectProcessor<BalanceCreateEffect>
    {
        public BalanceCreateEffectProcessor(BalanceCreateEffect effect, AccountStorage accountStorage)
            : base(effect, accountStorage)
        {

        }
        public override void CommitEffect()
        {
            Account.CreateBalance(Effect.Asset);
        }

        public override void RevertEffect()
        {
            Account.Balances.Remove(Account.GetBalance(Effect.Asset));
        }
    }
}
