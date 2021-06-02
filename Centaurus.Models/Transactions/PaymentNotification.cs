using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Contains info about external payment.
    /// </summary>
    [XdrContract]
    public class PaymentNotification
    {
        [XdrField(0)]
        public PaymentProvider Provider { get; set; }

        /// <summary>
        /// Payment cursor
        /// </summary>
        [XdrField(1)]
        public string Cursor { get; set; }

        /// <summary>
        /// List of payments witnessed by an auditor.
        /// </summary>
        [XdrField(2)]
        public List<PaymentBase> Items { get; set; } = new List<PaymentBase>();
    }
}
