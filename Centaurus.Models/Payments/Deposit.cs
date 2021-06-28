using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{    
    public class Deposit: PaymentBase
    {
        public override PaymentTypes Type => PaymentTypes.Deposit;

        [XdrField(1)]
        public int Asset { get; set; }

        [XdrField(2)]
        public long Amount { get; set; }

        [XdrField(3)]
        public RawPubKey Destination { get; set; }
    }
}
