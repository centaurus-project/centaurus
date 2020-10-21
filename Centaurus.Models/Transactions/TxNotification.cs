using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message from auditor to Alpha server that contains Stellar payments (obtained from the Horizon).
    /// </summary>
    public class TxNotification : Message
    {
        public override MessageTypes MessageType => MessageTypes.TxNotification;

        public override long MessageId => TxCursor;

        /// <summary>
        /// Transaction cursor
        /// </summary>
        [XdrField(0)]
        public long TxCursor { get; set; }

        /// <summary>
        /// List of payments witnessed by an auditor.
        /// </summary>
        [XdrField(2)]
        public List<PaymentBase> Payments { get; set; } = new List<PaymentBase>();
    }
}
