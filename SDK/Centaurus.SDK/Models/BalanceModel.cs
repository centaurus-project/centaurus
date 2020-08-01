using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.SDK.Models
{
    public class BalanceModel
    {
        public int Asset { get; set; }
        public long Amount { get; set; }
        public long Liabilities { get; set; }

        public decimal AmountInXlm
        {
            get => Amount > 0 ? Amount / StroopsPerXlm : 0;
        }

        public const int StroopsPerXlm = 10000000;
    }
}
