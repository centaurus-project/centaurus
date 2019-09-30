using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus.Models
{
    public class AuditLedgerSerializer : IXdrSerializer<LedgerUpdateNotification>
    {
        public void Deserialize(ref LedgerUpdateNotification value, XdrReader reader)
        {
            value.Ledger = reader.ReadUInt32();
            value.Payments = reader.ReadList<PaymentBase>();
        }

        public void Serialize(LedgerUpdateNotification value, XdrWriter writer)
        {
            writer.Write(value.Ledger);
            writer.Write(value.Payments);
        }
    }
}
