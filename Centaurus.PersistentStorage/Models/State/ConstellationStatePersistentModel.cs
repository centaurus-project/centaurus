using MessagePack;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Centaurus.PersistentStorage
{
    /// <summary>
    /// Contains last processed payment token
    /// </summary>
    [MessagePackObject]
    public class CursorsPersistentModel: IPersistentModel
    {
        [Key(0)]
        public Dictionary<string, string> Cursors { get; set; }

        [IgnoreMember]
        public byte[] Key 
        { 
            get
            {
                return new byte[] { };
            }
            set
            {

            }
        }

        [IgnoreMember]
        public string ColumnFamily => "cursors";
    }
}