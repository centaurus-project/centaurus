using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class Withdrawal: PaymentBase
    {
        public override PaymentTypes Type => PaymentTypes.Withdrawal;
    }
}
