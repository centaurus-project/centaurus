using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class RequestRateLimits
    {

        [XdrField(0)]
        public int MinuteLimit { get; set; }

        [XdrField(1)]
        public int HourLimit { get; set; }
    }
}
