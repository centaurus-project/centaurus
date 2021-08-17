using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class Account
    {
        [XdrField(0)]
        public ulong Id { get; set; }

        [XdrField(1)]
        public RawPubKey Pubkey { get; set; }

        [XdrField(2)]
        public ulong Nonce { get; set; }

        [XdrField(3)]
        public ulong AccountSequence { get; set; }

        [XdrField(4)]
        public List<Balance> Balances { get; set; }

        [XdrField(5)]
        public List<Order> Orders { get; set; }

        [XdrField(6, Optional = true)]
        public RequestRateLimits RequestRateLimits { get; set; }
    }
}
