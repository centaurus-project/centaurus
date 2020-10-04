using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Quantum created by Alpha server that contains aggregated tx updates provided by <see cref="TxNotification"/>.
    /// </summary>
    public class TxCommitQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.TxCommitQuantum;

        [XdrField(0)]
        public MessageEnvelope Source { get; set; }
    }
}
