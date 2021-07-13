using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Stellar.Models
{
    public class TxModel: TxModelBase
    {
        public long PagingToken { get; set; }

        public string EnvelopeXdr { get; set; }
    }
}
