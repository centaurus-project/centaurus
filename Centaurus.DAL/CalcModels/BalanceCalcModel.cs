using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL
{
    public class BalanceCalcModel: BaseCalcModel
    {
        public int AssetId { get; set; }

        public byte[] PubKey { get; set; }

        public long Amount { get; set; }

        public long Liabilities { get; set; }
    }
}
