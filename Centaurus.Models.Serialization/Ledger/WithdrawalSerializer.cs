using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class WithdrawalSerializer : IXdrSerializer<Withdrawal>
    {
        public void Deserialize(ref Withdrawal value, XdrReader reader)
        {
            value.Source = reader.Read<RawPubKey>();
        }

        public void Serialize(Withdrawal value, XdrWriter writer)
        {
            writer.Write(value.Source);
        }
    }
}
