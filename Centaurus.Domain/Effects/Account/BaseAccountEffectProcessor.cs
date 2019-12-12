using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseAccountEffectProcessor<T>: EffectProcessor<T>
        where T: Effect
    {
        protected AccountStorage accountStorage;

        public BaseAccountEffectProcessor(T effect, AccountStorage accountStorage)
            :base(effect)
        {
            if (accountStorage == null)
                throw new ArgumentNullException(nameof(accountStorage));
            this.accountStorage = accountStorage;
        }

        private Account account;
        public Account Account 
        {
            get
            {
                if (account == null)
                    account = accountStorage.GetAccount(Effect.Pubkey);
                return account;
            }
        }
    }
}
