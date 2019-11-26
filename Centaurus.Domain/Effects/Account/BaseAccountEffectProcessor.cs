using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseAccountEffectProcessor<T>: EffectProcessor<T>
        where T: Effect
    {
        protected Account account;

        public BaseAccountEffectProcessor(T effect, Account account)
            :base(effect)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));
            this.account = account;
        }
    }
}
