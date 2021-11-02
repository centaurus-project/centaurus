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
    public class QuantumSignatures: IApex
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        /// <summary>
        /// Contains all quantum's signatures except Alpha
        /// </summary>
        [XdrField(1)]
        public List<AuditorSignatureInternal> Signatures { get; set; }
    }
}
