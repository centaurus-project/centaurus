using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.NetSDK
{
    public static class MajorityHelper
    {
        public static int GetMajorityThreshold(int totalAuditorsCount)
        {
            return (int)(totalAuditorsCount / 2.0) + 1;
        }

        public static bool HasMajority(int signaturesCount, int totalAuditorsCount)
        {
            return signaturesCount >= GetMajorityThreshold(totalAuditorsCount);
        }
    }
}
