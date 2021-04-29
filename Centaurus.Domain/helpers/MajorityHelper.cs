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

        public static bool HasMajority(this ExecutionContext context, MessageEnvelope envelope)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            //imply that signatures are unique and were validated beforehand
            return envelope.Signatures.Count >= context.GetMajorityCount();
        }
    }
}
