using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.SDK
{
    public static class MajorityHelper
    {
        public static int GetMajorityCount(int totalAuditorsCount)
        {
            return totalAuditorsCount % 2 == 0
                ? (totalAuditorsCount / 2 + 1)
                : (int)Math.Ceiling(totalAuditorsCount / 2.0);
        }

        public static bool HasMajority(int signaturesCount, int totalAuditorsCount)
        {
            return signaturesCount >= GetMajorityCount(totalAuditorsCount);
        }
    }
}
