using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class RequestRateLimitsExtensions
    {
        /// <summary>
        /// Updates specified request rate limit object values.
        /// </summary>
        /// <param name="requestRateLimits">Target rate limit request object.</param>
        /// <param name="hourLimit">New hour limit.</param>
        /// <param name="minuteLimit">New minute limit.</param>
        public static void Update(this RequestRateLimits requestRateLimits, uint hourLimit, uint minuteLimit)
        {
            if (requestRateLimits == null)
                throw new ArgumentNullException(nameof(requestRateLimits));
            requestRateLimits.HourLimit = hourLimit;
            requestRateLimits.MinuteLimit = minuteLimit;
        }

        /// <summary>
        /// Updates specified request rate limit object values.
        /// </summary>
        /// <param name="requestRateLimits">Target rate limit request object.</param>
        /// <param name="newRequestRateLimits">New rate limit request object.</param>
        public static void Update(this RequestRateLimits requestRateLimits, RequestRateLimits newRequestRateLimits)
        {
            if (newRequestRateLimits == null)
                throw new ArgumentNullException(nameof(newRequestRateLimits));
            Update(requestRateLimits, newRequestRateLimits.HourLimit, newRequestRateLimits.MinuteLimit);
        }
    }
}
