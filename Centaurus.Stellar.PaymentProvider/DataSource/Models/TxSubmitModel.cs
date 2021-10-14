using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Stellar.Models
{
    public class TxSubmitModel: TxModelBase
    {
        public string ResultXdr { get; set; }
        public string FeeCharged { get; internal set; }
    }
}
