using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Ed25519Signature: IXdrSerializableModel
    {
        public RawPubKey Signer { get; set; }

        public byte[] Signature { get; set; }
    }
}
