using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class BaseBalanceEffect: Effect
    {
        [XdrField(0)]
        public ulong Amount { get; set; }

        [XdrField(1)]
        public string Asset { get; set; }
    }
}
