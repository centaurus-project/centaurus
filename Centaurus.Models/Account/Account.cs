using System.Collections.Generic;

namespace Centaurus.Models
{
    [XdrContract]
    public class Account
    {
        [XdrField(0)]
        public RawPubKey Pubkey { get; set; }

        [XdrField(1)]
        public ulong Nonce { get; set; }

        [XdrField(2)]
        public List<Balance> Balances { get; set; }

        public void Deserialize(ref Account value, XdrReader reader)
        {
            value.Pubkey = reader.ReadObject<RawPubKey>();
            value.Nonce = reader.ReadUInt64();
            value.Balances = reader.ReadList<Balance>();
        }

        public void Serialize(Account value, XdrWriter writer)
        {
            writer.WriteVariable(value.Pubkey.Data);
            writer.WriteUInt64(value.Nonce);
            writer.WriteObject(value.Balances);
        }
    }
}
