using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class QuantumProofPersistentModel
    {
        [Key(0)]
        public List<byte[]> EffectHashes { get; set; }

        [Key(1)]
        public List<SignaturePersistentModel> Signatures { get; set; }
    }
}