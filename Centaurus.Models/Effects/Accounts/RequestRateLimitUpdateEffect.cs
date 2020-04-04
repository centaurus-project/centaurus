using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class RequestRateLimitUpdateEffect : Effect
    {
        public override EffectTypes EffectType => EffectTypes.RequestRateLimitUpdate;

        [XdrField(0)]
        public RequestRateLimits RequestRateLimits { get; set; }

        [XdrField(1, Optional = true)]
        public RequestRateLimits PrevRequestRateLimits { get; set; }
    }
}
