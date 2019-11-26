using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class OrderModel
    {
        public ulong OrderId { get; set; }

        public double Price { get; set; }

        public long Amount { get; set; }

        public byte[] Pubkey { get; set; }
    }
}
