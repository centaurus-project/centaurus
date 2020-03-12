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

                int? hourLimit = account.RequestRateLimits?.HourLimit ?? Global.Constellation.RequestRateLimits?.HourLimit;
                if (hourLimit.HasValue && !IncSingleCounter(hourCounter, 60 * 1000 * 60, hourLimit.Value, requestDatetime, out error))
                    return false;

                int? minuteLimit = account.RequestRateLimits?.MinuteLimit ?? Global.Constellation.RequestRateLimits?.MinuteLimit;
                if (minuteLimit.HasValue && !IncSingleCounter(minuteCounter, 60 * 1000, minuteLimit.Value, requestDatetime, out error))
                    return false;

                return true;
            }
        }

        private bool IncSingleCounter(RequestCounter counter, int counterWindowPeriod, int maxAllowedRequestsCount, long requestDatetime, out string error)
        {
            error = null;
            if (maxAllowedRequestsCount < 0) //if less than zero than the counter is disabled 
                return true;

            if (counter.StartedAt + counterWindowPeriod < requestDatetime) //window is expired
                counter.Reset(requestDatetime);

            if (counter.Count + 1 > maxAllowedRequestsCount)
            {
                error = $"Too many requests. Max allowed request count is {maxAllowedRequestsCount} per {counterWindowPeriod}ms.";
                return false;
            }
            counter.IncRequestsCount();
            return true;
        }
    }
}
