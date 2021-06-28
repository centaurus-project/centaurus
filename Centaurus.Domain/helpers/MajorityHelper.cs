using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus
{
    //TODO: cache the results
    public static class MajorityHelper
    {
        public static int GetMajorityCount(this ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return GetMajorityCount(context.GetTotalAuditorsCount());
        }

        public static int GetTotalAuditorsCount(this ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return context.Constellation.Auditors.Count;
        }

        public static int GetMajorityCount(int totalAuditorsCount)
        {
            return totalAuditorsCount % 2 == 0
                ? (totalAuditorsCount / 2 + 1)
                : (int)Math.Ceiling(totalAuditorsCount / 2.0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context">Current execution context.</param>
        /// <param name="auditorsCount">Auditors count.</param>
        /// <param name="currentIncluded">Specifies if current server is included to auditors count.</param>
        /// <returns></returns>
        public static bool HasMajority(this ExecutionContext context, int auditorsCount, bool currentIncluded = true)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!currentIncluded)
                auditorsCount++;
            //+1 is current auditor
            return auditorsCount >= context.GetMajorityCount();
        }
    }
}
