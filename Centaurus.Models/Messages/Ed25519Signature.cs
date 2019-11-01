using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class Ed25519Signature
    {
        [XdrField(0)]
        public RawPubKey Signer { get; set; }

        [XdrField(1)]
        public byte[] Signature { get; set; }
    }
}
