using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class SingleNodeSignaturesBatch : Message
    {
        [XdrField(0)]
        public List<SingleNodeSignaturesBatchItem> Items { get; set; }
    }


    [XdrContract]
    public class SingleNodeSignaturesBatchItem: IApex
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public NodeSignatureInternal Signature { get; set; }
    }
}