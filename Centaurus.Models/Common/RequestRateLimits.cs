using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class RequestRateLimits
    {

        [XdrField(0)]
        public uint MinuteLimit { get; set; }

        [XdrField(1)]
        public uint HourLimit { get; set; }
    }
}
