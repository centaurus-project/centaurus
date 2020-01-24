using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Quantum created by Alpha server that contains aggregated ledger updates provided by <see cref="LedgerUpdateNotification"/>.
    /// </summary>
    public class LedgerCommitQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.LedgerCommitQuantum;

        [XdrField(0)]
        public MessageEnvelope Source { get; set; }
    }
}
