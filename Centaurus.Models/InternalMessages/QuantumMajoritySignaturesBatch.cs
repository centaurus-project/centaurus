using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantumMajoritySignaturesBatch : Message
    {
        [XdrField(0)]
        public List<QuantumSignatures> Signatures { get; set; }
    }

    [XdrContract]
    public class QuantumSignatures
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public List<AuditorSignatureInternal> Signatures { get; set; }
    }
}
