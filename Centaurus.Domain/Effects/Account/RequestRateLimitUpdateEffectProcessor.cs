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

        public RequestRateLimitUpdateEffectProcessor(RequestRateLimitUpdateEffect effect, Account account, RequestRateLimits globalRateLimits)
            : base(effect, account)
        {
            this.globalRateLimits = globalRateLimits ?? throw new ArgumentNullException(nameof(globalRateLimits)); 
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
                Account.RequestRateLimits = Effect.RequestRateLimits;
                Account.RequestCounter.SetLimits(Account.RequestRateLimits); //update reference
            }
            //update values
            Account.RequestRateLimits.Update(newValue);
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
