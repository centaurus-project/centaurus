using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class BaseBalanceEffect: Effect
    {
        [XdrField(0)]
        public long Amount { get; set; }

        [XdrField(1)]
        public int Asset { get; set; }
    }
}
