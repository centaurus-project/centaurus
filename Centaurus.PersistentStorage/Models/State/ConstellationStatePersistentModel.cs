using MessagePack;
using System.Buffers.Binary;

namespace Centaurus.PersistentStorage
{
    /// <summary>
    /// Contains last processed payment token
    /// </summary>
    [MessagePackObject]
    public class ProviderCursorPersistentModel: IPersistentModel
    {
        [Key(0)]
        public string Provider { get; set; }

        [Key(1)]
        public string Cursor { get; set; }

        public byte[] Key 
        { 
            get
            {
                var res = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(res, Provider.GetHashCode());
                return res;
            }
            set
            {

            }
        }

        public string ColumnFamily => "cursors";
    }
}