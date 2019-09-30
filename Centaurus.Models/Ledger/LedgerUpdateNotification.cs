using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus.Models
{
    /// <summary>
    /// Message from auditor to Alpha server that contains all Stellar payments included into the recent ledger (obtained from the Horizon).
    /// </summary>
    public class LedgerUpdateNotification : Message
    {
        public override MessageTypes MessageType => MessageTypes.LedgerUpdateNotification;
        
        /// <summary>
        /// Ledger sequence number.
        /// </summary>
        public uint Ledger { get; set; }

        /// <summary>
        /// List of payments witnessed by an auditor.
        /// </summary>
        public List<PaymentBase> Payments { get; set; } = new List<PaymentBase>();

        public override ulong MessageId => Ledger;
    }
}
