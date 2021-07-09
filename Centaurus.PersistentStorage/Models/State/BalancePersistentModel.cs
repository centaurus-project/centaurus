using MessagePack;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class BalancePersistentModel
    {
        [Key(0)]
        public string Asset { get; set; }

        [Key(1)]
        public ulong Amount { get; set; }
    }
}
