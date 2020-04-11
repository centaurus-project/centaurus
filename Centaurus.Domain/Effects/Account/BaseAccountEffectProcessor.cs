using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseAccountEffectProcessor<T>: EffectProcessor<T>
        where T: Effect
    {
        public BaseAccountEffectProcessor(T effect, Account account)
            :base(effect)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account)); ;
        }

        public Account Account { get; }
    }
}
