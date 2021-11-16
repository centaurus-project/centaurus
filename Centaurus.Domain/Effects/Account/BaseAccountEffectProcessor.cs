using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseAccountEffectProcessor<T>: ClientEffectProcessor<T>
        where T: AccountEffect
    {
        public BaseAccountEffectProcessor(T effect, Account account)
            :base(effect, account)
        {
        }
    }
}
