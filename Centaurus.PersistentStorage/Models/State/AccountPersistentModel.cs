using System.Collections.Generic;
using MessagePack;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class AccountPersistentModel : IPersistentModel, IBloomFilteredPersistentModel
    {
        [IgnoreMember]
        public byte[] AccountPubkey { get; set; }

        [Key(0)]
        public ulong AccountId { get; set; }

        [Key(1)]
        public ulong Nonce { get; set; }

        [Key(2)]
        public List<BalancePersistentModel> Balances { get; set; }

        [Key(3)]
        public List<OrderPersistentModel> Orders { get; set; }

        [Key(4)]
        public RequestRateLimitPersistentModel RequestRateLimits { get; set; } //TODO: make optional

        [IgnoreMember]
        public byte[] Key
        {
            get => AccountPubkey;
            set => AccountPubkey = value;
        }

        [IgnoreMember]
        public string ColumnFamily => "accounts";
    }
}
