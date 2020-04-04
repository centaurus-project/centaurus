using Centaurus.Models;

namespace Centaurus.Alpha
{
    public class ConstellationInitModel
    {
        public string[] Auditors { get; set; }

        public long MinAccountBalance { get; set; }

        public long MinAllowedLotSize { get; set; }

        public string[] Assets { get; set; }

        public RequestRateLimitsModel RequestRateLimits { get; set; }
    }
}
