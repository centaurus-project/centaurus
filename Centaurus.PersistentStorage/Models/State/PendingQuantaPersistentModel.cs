using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class PendingQuantaPersistentModel : IPersistentModel
    {
        [Key(0)]
        public List<PendingQuantumPersistentModel> Quanta { get; set; }

        [IgnoreMember]
        public byte[] Key
        {
            get => KeyValue;
            set { }
        }

        public static byte[] KeyValue { get; } = new byte[0];

        [IgnoreMember]
        public string ColumnFamily => "pendingQuanta";
    }
}
