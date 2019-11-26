using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class BaseOrderEffect: Effect
    {
        [XdrField(0)]
        public ulong OrderId { get; set; }

        [XdrField(1)]
        public double Price { get; set; }
    }
}
