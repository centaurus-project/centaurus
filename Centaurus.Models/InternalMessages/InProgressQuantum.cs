using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class InProgressQuantum : Message
    {
        public override MessageTypes MessageType => MessageTypes.ProcessedQuantum;

        [XdrField(0)]
        public MessageEnvelope QuantumEnvelope { get; set; }

        public Quantum Quantum => (Quantum)QuantumEnvelope.Message;

        [XdrField(1)]
        public List<AuditorSignature> Signatures { get; set; }
    }

    [XdrContract]
    public class AuditorSignature
    {
        [XdrField(0)]
        public TinySignature EffectsSignature { get; set; }

        [XdrField(1, Optional = true)]
        public byte[] TxSigner { get; set; }

        [XdrField(2, Optional = true)]
        public byte[] TxSignature { get; set; }
    }
}
