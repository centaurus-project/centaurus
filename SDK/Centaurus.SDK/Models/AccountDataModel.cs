using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.SDK.Models
{
    public class AccountDataModel
    {
        public long Nonce { get; set; }

        public List<BalanceModel> Balances { get; set; }
    }
}
