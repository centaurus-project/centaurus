using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class AuditorResultMessage
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public byte[] Signature { get; set; }

        [XdrField(2, Optional = true)]
        public byte[] TxSigner { get; set; }

        [XdrField(3, Optional = true)]
        public byte[] TxSignature { get; set; }
    }
}
