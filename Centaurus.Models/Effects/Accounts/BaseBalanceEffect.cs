using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class BaseBalanceEffect: AccountEffect
    {
        [XdrField(0)]
        public string Asset { get; set; }
    }
}
