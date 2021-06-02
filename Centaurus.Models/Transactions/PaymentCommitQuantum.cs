using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Quantum created contains aggregated payments provided by <see cref="PaymentNotification"/>.
    /// </summary>
    public class PaymentCommitQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.TxCommitQuantum;

        [XdrField(0)]
        public PaymentNotification Source { get; set; }
    }
}