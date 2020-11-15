using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Analytics
{
    public class Trade
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public double Price { get; set; }

        public double BaseAmount { get; set; }

        /// <summary>
        /// TimeStamp in ticks. We need it for sorting
        /// </summary>
        public long Timestamp { get; set; }
    }
}