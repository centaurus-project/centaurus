using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class AccountRequestCounter
    {
        private Account account;
        private RequestCounter minuteCounter;
        private RequestCounter hourCounter;

        public AccountRequestCounter(Account _account)
        {
            account = _account;
            minuteCounter = new RequestCounter();
            hourCounter = new RequestCounter();
        }

        public bool IncRequestCount(long requestDatetime, out string error)
        {
            lock (this)
            {
                error = null;

                uint? hourLimit = account.RequestRateLimits?.HourLimit ?? Global.Constellation.RequestRateLimits?.HourLimit;
                var hourInTicks = (long)60 * 1000 * 60 * 10_000;
                if (!IncSingleCounter(hourCounter, hourInTicks, hourLimit.Value, requestDatetime, out error))
                    return false;

                var minuteInTicks = (long)60 * 1000 * 10_000;
                uint? minuteLimit = account.RequestRateLimits?.MinuteLimit ?? Global.Constellation.RequestRateLimits?.MinuteLimit;
                if (!IncSingleCounter(minuteCounter, minuteInTicks, minuteLimit.Value, requestDatetime, out error))
                    return false;

                return true;
            }
        }

        private bool IncSingleCounter(RequestCounter counter, long counterWindowPeriod, uint maxAllowedRequestsCount, long requestDatetime, out string error)
        {
            error = null;
            if (maxAllowedRequestsCount < 0) //if less than zero than the counter is disabled 
                return true;

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
