using Centaurus.Xdr;
using System.Collections.Generic;

namespace Centaurus.Models
{
    public class AccountDataResponse: QuantumResultMessageBase
    {
        [XdrField(0)]
        public List<Balance> Balances { get; set; }

        [XdrField(1)]
        public List<Order> Orders { get; set; }

        [XdrField(2)]
        public ulong Sequence { get; set; }
    }
}