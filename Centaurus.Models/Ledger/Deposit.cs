using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Deposit: PaymentBase
    {
        public override PaymentTypes Type => PaymentTypes.Deposit;
    }
}
