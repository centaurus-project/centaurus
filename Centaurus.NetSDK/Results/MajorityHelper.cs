using Centaurus.Models;

namespace Centaurus.NetSDK
{
    public static class MajorityHelper
    {
        public static int GetMajorityThreshold(int totalAuditorsCount)
        {
            return (int)(totalAuditorsCount / 2.0) + 1;
        }

        public static bool HasMajority(this MessageEnvelope envelope, int totalAuditorsCount)
        {
            //imply that signatures are unique and were validated beforehand
            var auditorsSignaturesCount = envelope.Signatures.Count - 1; //1 signature belongs to Alpha
            return auditorsSignaturesCount >= GetMajorityThreshold(totalAuditorsCount);
        }
    }
}
