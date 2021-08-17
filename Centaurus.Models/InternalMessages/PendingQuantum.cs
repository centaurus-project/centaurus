using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class PendingQuantum : Message
    {
        [XdrField(0)]
        public Quantum Quantum { get; set; }

        [XdrField(1)]
        public List<AuditorSignature> Signatures { get; set; }
    }

    [XdrContract]
    public class AuditorSignature
    {
        [XdrField(0)]
        public TinySignature PayloadSignature { get; set; }

        [XdrField(1, Optional = true)]
        public byte[] TxSigner { get; set; }

        [XdrField(2, Optional = true)]
        public byte[] TxSignature { get; set; }
    }
}
