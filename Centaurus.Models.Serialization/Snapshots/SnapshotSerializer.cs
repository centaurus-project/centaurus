using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Models
{
    public class SnapshotSerializer : IXdrSerializer<Snapshot>
    {
        public void Deserialize(ref Snapshot value, XdrReader reader)
        {
            value.Id = reader.ReadUInt64();
            value.Apex = reader.ReadUInt64();
            value.Settings = reader.Read<ConstellationSettings>();
            value.Ledger = reader.ReadInt64();
            value.VaultSequence = reader.ReadInt64();
            value.Accounts = reader.ReadList<Account>();
            value.Orders = reader.ReadList<Order>();
            value.Withdrawals = reader.ReadList<PaymentRequestBase>();
            value.Confirmation = reader.ReadOptional<MessageEnvelope>();
        }

        public void Serialize(Snapshot value, XdrWriter writer)
        {
            writer.Write(value.Id);
            writer.Write(value.Apex);
            writer.Write(value.Settings);
            writer.Write(value.Ledger);
            writer.Write(value.VaultSequence);
            writer.Write(value.Accounts);
            writer.Write(value.Orders);
            writer.Write(value.Withdrawals);
            writer.WriteOptional(value.Confirmation);
        }
    }
}
