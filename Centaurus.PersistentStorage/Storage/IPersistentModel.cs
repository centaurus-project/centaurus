using MessagePack;

namespace Centaurus.PersistentStorage
{
    public interface IPersistentModel
    {
        [IgnoreMember]
        public byte[] Key { get; set; }

        [IgnoreMember]
        public string ColumnFamily { get; }

        public byte[] SerializeValue()
        {
            return MessagePackSerializer.Serialize(GetType(), this);
        }

        public static T Deserialize<T>(byte[] key, byte[] data) where T : IPersistentModel
        {
            var res = MessagePackSerializer.Deserialize<T>(data);
            res.Key = key;
            return res;
        }
    }

    public interface IBloomFilteredPersistentModel
    {
    }

    public interface IPrefixedPersistentModel: IBloomFilteredPersistentModel
    {

        [IgnoreMember]
        public uint PrefixLength { get; }
    }
}
