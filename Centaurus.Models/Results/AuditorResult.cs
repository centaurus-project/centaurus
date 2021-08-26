using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class AuditorResult
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public AuditorSignatureInternal Signature { get; set; }
    }
}
