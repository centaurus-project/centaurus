using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantumContainer
    {
        [XdrField(0)]
        public MessageEnvelope Quantum { get; set; }

        [XdrField(1)]
        public List<Effect> Effects { get; set; }
    }
}
