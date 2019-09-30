using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Withdrawal: PaymentBase
    {
        public RawPubKey Source { get; set; }

        public override PaymentTypes Type => PaymentTypes.Withdrawal;
    }
}
