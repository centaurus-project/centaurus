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
        public AuditorSignature Signature { get; set; }
    }
}
