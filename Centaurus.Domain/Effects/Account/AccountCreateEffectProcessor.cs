using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class AccountCreateEffectProcessor : EffectProcessor<AccountCreateEffect>
    {
        private AccountStorage accountStorage;
        private RequestRateLimits requestRateLimits;

        public AccountCreateEffectProcessor(AccountCreateEffect effect, AccountStorage accountStorage, RequestRateLimits requestRateLimits)
            : base(effect)
        {
            this.accountStorage = accountStorage ?? throw new ArgumentNullException(nameof(accountStorage));
            this.requestRateLimits = requestRateLimits ?? throw new ArgumentNullException(nameof(requestRateLimits));
        }
        public override void CommitEffect()
        {
            MarkAsProcessed();
            accountStorage.CreateAccount(Effect.Account, requestRateLimits);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            accountStorage.RemoveAccount(Effect.Account);
        }
    }
}
