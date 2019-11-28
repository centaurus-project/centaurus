using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class WithdrawalModel
    {
        public ulong Apex { get; set; }

        public byte[] TransactionHash { get; set; }

        public byte[] RawWithdrawal { get; set; }
    }
}
