using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL
{
    public class WithdrawalCalcModel: BaseCalcModel
    {
        public ulong Apex { get; set; }

        public byte[] TransactionHash { get; set; }
    }
}
