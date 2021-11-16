using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage
{

    [MessagePackObject]
    public class PendingQuantumPersistentModel
    {
        [Key(0)]
        public byte[] RawQuantum { get; set; }

        [Key(1)]
        public List<SignatureModel> Signatures { get; set; }
    }
}
