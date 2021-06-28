using Centaurus.Models;
using System;

namespace Centaurus.Domain.Models
{
    public class AccountRequestCounter
    {
        private Account account;
        private RequestCounter minuteCounter;
        private RequestCounter hourCounter;

        private RequestRateLimits requestRateLimits;

        public AccountRequestCounter(Account _account, RequestRateLimits requestRateLimits)
        {
            account = _account ?? throw new ArgumentNullException(nameof(_account));
            minuteCounter = new RequestCounter();
            hourCounter = new RequestCounter();
            SetLimits(requestRateLimits);
        }

        public void SetLimits(RequestRateLimits requestRateLimits)
        {
            this.requestRateLimits = requestRateLimits ?? throw new ArgumentNullException(nameof(requestRateLimits));
        }

        public bool IncRequestCount(long requestDatetime, out string error)
        {
            lock (this)
            {
                error = null;

                var hourInTicks = (long)60 * 1000 * 60 * 10_000;
                if (!IncSingleCounter(hourCounter, hourInTicks, requestRateLimits.HourLimit, requestDatetime, out error))
                    return false;

                var minuteInTicks = (long)60 * 1000 * 10_000;
                if (!IncSingleCounter(minuteCounter, minuteInTicks, requestRateLimits.MinuteLimit, requestDatetime, out error))
                    return false;

                return true;
            }
        }

        private bool IncSingleCounter(RequestCounter counter, long counterWindowPeriod, uint maxAllowedRequestsCount, long requestDatetime, out string error)
        {
            error = null;
            if ((counter.StartedAt + counterWindowPeriod) < requestDatetime) //window is expired
                counter.Reset(requestDatetime);

            if (counter.Count + 1 > maxAllowedRequestsCount)
            {
                error = $"Too many requests. Max allowed request count is {maxAllowedRequestsCount} per {counterWindowPeriod/10_000}ms.";
                return false;
            }
            counter.IncRequestsCount();
            return true;
        }
    }
}
