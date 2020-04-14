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
            this.accountStorage = accountStorage ?? throw new ArgumentNullException(nameof(accountStorage));
        }
        public override void CommitEffect()
        {
            MarkAsProcessed();
            accountStorage.CreateAccount(Effect.Pubkey);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            accountStorage.RemoveAccount(Effect.Pubkey);
        }
    }
}
