using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class WithdrawalModel
    {
        //it stores ulong
        public long Apex { get; set; }

        public byte[] TransactionHash { get; set; }

        public byte[] RawWithdrawal { get; set; }
    }
}
