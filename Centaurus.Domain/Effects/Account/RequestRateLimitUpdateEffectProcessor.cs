using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class RequestRateLimitUpdateEffectProcessor : BaseAccountEffectProcessor<RequestRateLimitUpdateEffect>
    {
        public RequestRateLimitUpdateEffectProcessor(RequestRateLimitUpdateEffect effect, AccountStorage accountStorage)
            : base(effect, accountStorage)
        {

        }

        public override void CommitEffect()
        {
            Account.RequestRateLimits = Effect.RequestRateLimits;
        }

        public override void RevertEffect()
        {
            Account.RequestRateLimits = Effect.PrevRequestRateLimits;
        }
    }
}
