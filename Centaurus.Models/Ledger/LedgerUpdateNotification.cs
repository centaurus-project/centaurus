using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message from auditor to Alpha server that contains all Stellar payments included into the recent ledger (obtained from the Horizon).
    /// </summary>
    public class LedgerUpdateNotification : Message
    {
        public override MessageTypes MessageType => MessageTypes.LedgerUpdateNotification;

        public override long MessageId => LedgerFrom;

        /// <summary>
        /// Ledgers range start.
        /// </summary>
        [XdrField(0)]
        public long LedgerFrom { get; set; }

        /// <summary>
        /// Ledgers range end.
        /// </summary>
        [XdrField(1)]
        public long LedgerTo { get; set; }

        /// <summary>
        /// List of payments witnessed by an auditor.
        /// </summary>
        [XdrField(2)]
        public List<PaymentBase> Payments { get; set; } = new List<PaymentBase>();
    }
}
