using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class RequestRateLimitUpdateEffectProcessor : BaseAccountEffectProcessor<RequestRateLimitUpdateEffect>
    {
        private RequestRateLimits globalRateLimits;

        public RequestRateLimitUpdateEffectProcessor(RequestRateLimitUpdateEffect effect, AccountWrapper account, RequestRateLimits globalRateLimits)
            : base(effect, account)
        {
            this.globalRateLimits = globalRateLimits ?? throw new ArgumentNullException(nameof(globalRateLimits)); 
        }

        private void Update(RequestRateLimits newValue)
        {
            if (newValue == null)
            {
                AccountWrapper.Account.RequestRateLimits = null;
                AccountWrapper.Account.RequestRateLimits.Update(globalRateLimits);
                return;
            }
            if (AccountWrapper.Account.RequestRateLimits == null)
            {
                AccountWrapper.Account.RequestRateLimits = new RequestRateLimits();
                AccountWrapper.RequestCounter.SetLimits(AccountWrapper.Account.RequestRateLimits); //update reference
            }
            //update values
            AccountWrapper.Account.RequestRateLimits.Update(newValue);
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            Update(Effect.RequestRateLimits);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            Update(Effect.PrevRequestRateLimits);
        }
    }
}
