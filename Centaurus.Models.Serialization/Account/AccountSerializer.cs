using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AccountSerializer : IXdrSerializer<Account>
    {
        public void Deserialize(ref Account value, XdrReader reader)
        {
            value.Pubkey = reader.Read<RawPubKey>();
            value.Nonce = reader.ReadUInt64();
            value.Balances = reader.ReadList<Balance>();
        }

        public void Serialize(Account value, XdrWriter writer)
        {
            writer.Write(value.Pubkey.Data);
            writer.Write(value.Nonce);
            writer.Write(value.Balances);
        }
    }
}
