using System.Collections.Generic;
using Centaurus.Xdr;

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

        [XdrField(3, Optional = true)]
        public RequestRateLimits RequestRateLimits { get; set; }
    }
}
