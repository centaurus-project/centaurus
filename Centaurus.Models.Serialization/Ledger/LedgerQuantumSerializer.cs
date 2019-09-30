using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus.Models
{
    public class LedgerQuantumSerializer : IXdrSerializer<LedgerCommitQuantum>
    {
        public void Deserialize(ref LedgerCommitQuantum value, XdrReader reader)
        {
            value.Source = reader.Read<MessageEnvelope>();
        }

        public void Serialize(LedgerCommitQuantum value, XdrWriter writer)
        {
            writer.Write(value.Source);
        }
    }
}
