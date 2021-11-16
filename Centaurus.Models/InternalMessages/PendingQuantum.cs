using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class PendingQuantum
    {
        public Quantum Quantum { get; set; }

        public List<AuditorSignatureInternal> Signatures { get; set; }
    }
}
