using Centaurus.Xdr;
using System.Collections.Generic;

namespace Centaurus.Models
{
    public class AccountDataResponse: ResultMessage
    {
        public override MessageTypes MessageType => MessageTypes.AccountDataResponse;

        [XdrField(0)]
        public List<Balance> Balances { get; set; }

        [XdrField(1)]
        public List<Order> Orders { get; set; }
    }
}
