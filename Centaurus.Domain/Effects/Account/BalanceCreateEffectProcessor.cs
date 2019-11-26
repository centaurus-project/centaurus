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
        public override UpdatedObject[] CommitEffect()
        {
            var balance = account.CreateBalance(Effect.Asset);
            return new UpdatedObject[] { new UpdatedObject(balance) };
        }

        public override void RevertEffect()
        {
            account.Balances.Remove(account.GetBalance(Effect.Asset));
        }

        public static BalanceCreateEffectProcessor GetProcessor(ulong apex,Account account, int asset)
        {
            return GetProcessor(
                new BalanceCreateEffect { Pubkey = account.Pubkey, Asset = asset, Apex = apex },
                account
            );
        }

        public static BalanceCreateEffectProcessor GetProcessor(BalanceCreateEffect effect, Account account)
        {
            return new BalanceCreateEffectProcessor(effect, account);
        }
    }
}
