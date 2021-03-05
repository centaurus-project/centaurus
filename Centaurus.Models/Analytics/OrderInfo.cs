using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public enum OrderState
    { 
        New = 0,
        Updated = 1,
        Deleted = 2
    }

    public class OrderInfo
    {
        public ulong OrderId { get; set; }

        public int Market { get; set; }

        public OrderSide Side { get; set; }

        public double Price { get; set; }

        public long AmountDiff { get; set; }

        public long QuoteAmountDiff { get; set; }

        public OrderState State { get; set; }
    }
}