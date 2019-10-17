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
            value.LedgerFrom = reader.ReadUInt32();
            value.LedgerTo = reader.ReadUInt32();
            value.Payments = reader.ReadList<PaymentBase>();
        }

        public void Serialize(LedgerUpdateNotification value, XdrWriter writer)
        {
            writer.Write(value.LedgerFrom);
            writer.Write(value.LedgerTo);
            writer.Write(value.Payments);
        }
    }
}
