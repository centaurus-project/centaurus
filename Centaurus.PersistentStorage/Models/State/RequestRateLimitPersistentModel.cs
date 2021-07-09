using MessagePack;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class RequestRateLimitPersistentModel
    {
        [Key(0)]
        public uint HourLimit { get; set; }

        [Key(1)]
        public uint MinuteLimit { get; set; }
    }
}
