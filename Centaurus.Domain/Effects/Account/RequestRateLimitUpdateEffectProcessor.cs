using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class RequestRateLimitUpdateEffectProcessor : BaseAccountEffectProcessor<RequestRateLimitUpdateEffect>
    {
        private RequestRateLimits globalRateLimits;

        public RequestRateLimitUpdateEffectProcessor(RequestRateLimitUpdateEffect effect, AccountStorage accountStorage, RequestRateLimits globalRateLimits)
            : base(effect, accountStorage)
        {
            this.globalRateLimits = globalRateLimits;
        }

        private void Update(RequestRateLimits newValue)
        {
            if (newValue == null)
            {
                Account.RequestRateLimits = null;
                Account.RequestRateLimits.Update(globalRateLimits);
                return;
            }
            if (Account.RequestRateLimits == null)
            {
                Account.RequestRateLimits = new RequestRateLimits();
                AccountWrapper.RequestCounter.SetLimits(Account.RequestRateLimits); //update reference
            }
            //update values
            Account.RequestRateLimits.Update(newValue);
        }

        public override void CommitEffect()
        {
            Update(Effect.RequestRateLimits);
        }

        public override void RevertEffect()
        {
            Update(Effect.PrevRequestRateLimits);
        }
    }
}
