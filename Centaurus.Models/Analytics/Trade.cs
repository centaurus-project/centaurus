using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Trade
    {
        public string Asset { get; set; }

        public ulong Amount { get; set; }

        public double Price { get; set; }

        public ulong QuoteAmount { get; set; }

        public DateTime TradeDate { get; set; } 
    }
}