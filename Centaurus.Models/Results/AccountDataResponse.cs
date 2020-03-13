using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AccountDataResponse: ResultMessage
    {
        public override MessageTypes MessageType => MessageTypes.AccountDataResponse;

        [XdrField(0)]
        public List<Balance> Balances { get; set; }

        [XdrField(0)]
        public List<Order> Orders { get; set; }
    }
}
