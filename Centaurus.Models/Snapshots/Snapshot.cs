using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Models
{
    public class Snapshot: IXdrSerializableModel
    {
        public ulong Id { get; set; }

        public ulong Apex { get; set; }

        public long Ledger { get; set; }

        public long VaultSequence { get; set; }

        public ConstellationSettings Settings { get; set; }

        public List<Account> Accounts { get; set; }

        public List<Order> Orders { get; set; }

        /// <summary>
        /// Pending withdrawals
        /// </summary>
        public List<PaymentRequestBase> Withdrawals { get; set; }

        /// <summary>
        /// Envelope for <see cref="SnapshotQuantum"/> with aggregated auditor signatures.
        /// </summary>
        public MessageEnvelope Confirmation { get; set; }
    }
}
