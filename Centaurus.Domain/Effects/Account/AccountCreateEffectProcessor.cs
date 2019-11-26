using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class AccountCreateEffectProcessor : EffectProcessor<AccountCreateEffect>
    {
        private AccountStorage accountStorage;

        public AccountCreateEffectProcessor(AccountCreateEffect effect, AccountStorage accountStorage)
            : base(effect)
        {
            this.accountStorage = accountStorage;
        }
        public override UpdatedObject[] CommitEffect()
        {
            var account = accountStorage.CreateAccount(Effect.Pubkey);
            return new UpdatedObject[] { new UpdatedObject(account, false) };
        }

        public override void RevertEffect()
        {
            accountStorage.RemoveAccount(Effect.Pubkey);
        }

        public static AccountCreateEffectProcessor GetProcessor(ulong apex, AccountStorage accountStorage, RawPubKey publicKey)
        {
            return GetProcessor(
                new AccountCreateEffect { Pubkey = publicKey, Apex = apex },
                accountStorage
            );
        }

        public static AccountCreateEffectProcessor GetProcessor(AccountCreateEffect effect, AccountStorage accountStorage)
        {
            return new AccountCreateEffectProcessor(
                effect,
                accountStorage
            );
        }
    }
}
