using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseAccountEffectProcessor<T>: EffectProcessor<T>
        where T: Effect
    {
        public BaseAccountEffectProcessor(T effect)
            :base(effect)
        {
        }

        public Account Account => Effect.AccountWrapper?.Account;
    }
}
