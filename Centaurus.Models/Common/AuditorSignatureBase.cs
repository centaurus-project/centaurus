using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion(0, typeof(AuditorSignatureInternal))]
    [XdrUnion(1, typeof(AuditorSignature))]
    public class AuditorSignatureBase
    {
        [XdrField(0)]
        public int AuditorId { get; set; }

        [XdrField(1)]
        public TinySignature PayloadSignature { get; set; }
    }

    public class AuditorSignatureInternal: AuditorSignatureBase
    {

        [XdrField(0, Optional = true)]
        public byte[] TxSigner { get; set; }

        [XdrField(1, Optional = true)]
        public byte[] TxSignature { get; set; }
    }

    public class AuditorSignature : AuditorSignatureBase
    {
    }
}