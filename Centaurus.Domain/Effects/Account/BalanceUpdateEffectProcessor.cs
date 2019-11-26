using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class BalanceUpdateEffectProcesor : BaseAccountEffectProcessor<BalanceUpdateEffect>
    {
        public BalanceUpdateEffectProcesor(BalanceUpdateEffect effect, Account account)
            : base(effect, account)
        {

        }
        public override UpdatedObject[] CommitEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.UpdateBalance(Effect.Amount);
            return new UpdatedObject[] { new UpdatedObject(balance) };
        }

        public override void RevertEffect()
        {
            var balance = account.GetBalance(Effect.Asset);
            balance.UpdateBalance(-Effect.Amount);
        }

        public static BalanceUpdateEffectProcesor GetProcessor(ulong apex, Account account, int asset, long amount)
        {
            return new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect { Pubkey = account.Pubkey, Amount = amount, Asset = asset, Apex = apex },
                account
            );
        }

        public static BalanceUpdateEffectProcesor GetProcessor(BalanceUpdateEffect effect, Account account)
        {
            return new BalanceUpdateEffectProcesor(effect, account);
        }
    }
}
