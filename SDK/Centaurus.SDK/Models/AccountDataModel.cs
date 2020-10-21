using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.SDK.Models
{
    public class AccountDataModel
    {
        public long Nonce { get; set; }

        public Dictionary<int, BalanceModel> Balances { get; set; }

        public Dictionary<ulong, OrderModel> Orders { get; set; }
    }
}
