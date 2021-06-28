using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class TxSignature
    {
        [XdrField(0)]
        public byte[] Signer { get; set; }

        [XdrField(1)]
        public byte[] Signature { get; set; }
    }
}
