using MessagePack;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class AssetPersistentModel
    {
        [Key(0)]
        public string Code { get; set; }

        [Key(1)]
        public bool IsSuspended { get; set; }
    }
}
