using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AuditorSignaturesBatch : Message
    {
        [XdrField(0)]
        public List<AuditorResult> AuditorResultMessages { get; set; }
    }
}