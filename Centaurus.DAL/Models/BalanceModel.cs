using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class BalanceModel
    {
        public int AssetId { get; set; }

        public byte[] Account { get; set; }

        public long Amount { get; set; }

        public long Liabilities { get; set; }
    }
}
