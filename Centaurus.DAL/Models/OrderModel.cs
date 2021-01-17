using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class OrderModel
    {
        public long Id { get; set; }

        public double Price { get; set; }

        public long Amount { get; set; }

        public int Account { get; set; }
    }
}
