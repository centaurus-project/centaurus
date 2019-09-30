using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class BalanceSerializer : IXdrSerializer<Balance>
    {
        public void Deserialize(ref Balance value, XdrReader reader)
        {
            value.Asset = reader.ReadInt32();
            value.Amount = reader.ReadInt64();
            value.Liabilities = reader.ReadInt64();
        }

        public void Serialize(Balance value, XdrWriter writer)
        {
            writer.Write(value.Asset);
            writer.Write(value.Amount);
            writer.Write(value.Liabilities);
        }
    }
}
