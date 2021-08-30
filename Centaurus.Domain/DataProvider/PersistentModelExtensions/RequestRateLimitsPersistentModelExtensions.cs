using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class RequestRateLimitsPersistentModelExtensions
    {
        public static RequestRateLimitPersistentModel ToPersistentModel(this RequestRateLimits requestRateLimit)
        {
            if (requestRateLimit == null)
                throw new ArgumentNullException(nameof(requestRateLimit));

            return new RequestRateLimitPersistentModel { HourLimit = requestRateLimit.HourLimit, MinuteLimit = requestRateLimit.MinuteLimit };
        }
        public static RequestRateLimits ToDomainModel(this RequestRateLimitPersistentModel requestRateLimit)
        {
            if (requestRateLimit == null)
                throw new ArgumentNullException(nameof(requestRateLimit));

            return new RequestRateLimits { HourLimit = requestRateLimit.HourLimit, MinuteLimit = requestRateLimit.MinuteLimit };
        }
    }
}
