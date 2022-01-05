using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class MajoritySignaturesBatch : Message
    {
        [XdrField(0)]
        public List<MajoritySignaturesBatchItem> Items { get; set; }
    }

    [XdrContract]
    public class MajoritySignaturesBatchItem: IApex
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public List<NodeSignatureInternal> Signatures { get; set; }
    }
}
