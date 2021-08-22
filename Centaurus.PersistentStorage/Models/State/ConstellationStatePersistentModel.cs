using MessagePack;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Centaurus.PersistentStorage
{
    /// <summary>
    /// Contains last processed payment token
    /// </summary>
    [MessagePackObject]
    public class CursorsPersistentModel : IPersistentModel
    {
        [Key(0)]
        public Dictionary<string, string> Cursors { get; set; }

        [IgnoreMember]
        public byte[] Key
        {
            get => new byte[0];
            set { }
        }

        [IgnoreMember]
        public string ColumnFamily => "cursors";
    }
}