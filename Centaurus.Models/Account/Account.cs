using System.Collections.Generic;

namespace Centaurus.Models
{
    public class Account: IXdrSerializableModel
    {
        public RawPubKey Pubkey { get; set; }

        public ulong Nonce { get; set; }

        public List<Balance> Balances { get; set; }
    }
}
