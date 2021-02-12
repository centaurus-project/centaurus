using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class Order
    {
        [XdrField(0)]
        public ulong OrderId { get; set; }

        [XdrField(1)]
        public double Price { get; set; }

        [XdrField(2)]
        public long Amount { get; set; }

        public Account Account { get; set; }

        public Order Next { get; set; }

        public Order Prev { get; set; }

        public Order Clone()
        {
            return (Order)MemberwiseClone();
        }
    }
}
