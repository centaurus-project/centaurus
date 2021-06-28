 using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Stellar.Models
{
    public abstract class TxModelBase
    {
        public string Hash { get; set; }

        public bool IsSuccess { get; set; }
    }
}
