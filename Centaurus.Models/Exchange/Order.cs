using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Order: IXdrSerializableModel
    {
        public ulong OrderId { get; set; }

        public double Price { get; set; }

        public long Amount { get; set; }

        public Order Next { get; set; }

        public Order Prev { get; set; }

        public RawPubKey Pubkey { get; set; } //TODO: use account reference instead
    }
}
