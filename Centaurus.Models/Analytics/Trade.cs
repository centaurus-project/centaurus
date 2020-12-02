using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Trade
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public double Price { get; set; }

        public double BaseAmount { get; set; }

        public DateTime TradeDate { get; set; } 
    }
}