using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceUpdateEffectProccesor : BaseAccountEffectProcessor<BalanceUpdateEffect>
    {
        public BalanceUpdateEffectProccesor(BalanceUpdateEffect effect, Account account)
            : base(effect, account)
        {

        }
        public override void CommitEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount);
        }

        public override void RevertEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.UpdateBalance(-Effect.Amount);
        }

        public static BalanceUpdateEffectProccesor GetProcessor(ulong apex, Account account, int asset, long amount)
        {
            return new BalanceUpdateEffectProccesor(
                new BalanceUpdateEffect { Pubkey = account.Pubkey, Amount = amount, Asset = asset, Apex = apex },
                account
            );
        }
    }
}
