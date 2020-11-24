using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderInfo
    {
        public ulong OrderId { get; set; }

        public int Market { get; set; }

        public OrderSide Side { get; set; }

        public double Price { get; set; }

        public long Amount { get; set; }

        public bool IsDeleted { get; set; }
    }
}