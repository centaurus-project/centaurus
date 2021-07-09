using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class SignaturePersistentModel
    {
        [Key(0)]
        public byte[] Signer { get; set; }

        [Key(1)]
        public byte[] Data { get; set; }
    }
}
