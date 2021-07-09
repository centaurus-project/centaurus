using MessagePack;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class OrderPersistentModel
    {
        [Key(0)]
        public ulong Apex { get; set; }

        [Key(1)]
        public double Price { get; set; }

        [Key(2)]
        public ulong Amount { get; set; }

        [Key(3)]
        public ulong QuoteAmount { get; set; }

        [Key(4)]
        public string Asset { get; set; }

        [Key(5)]
        public int Side { get; set; }
    }
}
