using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceCreateEffectProcessor : BaseAccountEffectProcessor<BalanceCreateEffect>
    {
        public BalanceCreateEffectProcessor(BalanceCreateEffect effect, Account account)
            : base(effect, account)
        {

        }
        public override void CommitEffect()
        {
            account.CreateBalance(Effect.Asset);
        }

        public override void RevertEffect()
        {
            account.Balances.Remove(account.GetBalance(Effect.Asset));
        }

        public static BalanceCreateEffectProcessor GetProcessor(ulong apex,Account account, int asset)
        {
            return new BalanceCreateEffectProcessor(
                new BalanceCreateEffect { Pubkey = account.Pubkey, Asset = asset, Apex = apex },
                account
            );
        }
    }
}
