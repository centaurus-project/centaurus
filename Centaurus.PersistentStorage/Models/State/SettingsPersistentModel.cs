using MessagePack;
using System.Collections.Generic;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class SettingsPersistentModel : IPersistentModel
    {
        [IgnoreMember]
        public ulong Apex { get; set; }

        [Key(0)]
        public byte[] Alpha { get; set; }

        [Key(1)]
        public List<AuditorModel> Auditors { get; set; }

        [Key(2)]
        public ulong MinAccountBalance { get; set; }

        [Key(3)]
        public ulong MinAllowedLotSize { get; set; }

        [Key(4)]
        public RequestRateLimitPersistentModel RequestRateLimits { get; set; }

        [Key(5)]
        public List<AssetPersistentModel> Assets { get; set; }

        [Key(6)]
        public List<ProviderSettingsPersistentModel> Providers { get; set; }

        [IgnoreMember]
        public byte[] Key
        {
            get => ApexConverter.EncodeApex(Apex);
            set => Apex = ApexConverter.DecodeApex(value);
        }

        [IgnoreMember]
        public string ColumnFamily => "settings";
    }


    [MessagePackObject]
    public class AuditorModel
    {
        [Key(0)]
        public byte[] PubKey { get; set; }

        [Key(1)]
        public string Address { get; set; }
    }
}
