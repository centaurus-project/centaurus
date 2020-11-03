using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models.Analytics
{
    public class TradeModel
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public double Price { get; set; }

        public double BaseAmount { get; set; }

        public long Timestamp { get; set; }
    }
}
