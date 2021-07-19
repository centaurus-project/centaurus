﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider.Models
{
    public class WithdrawalRequestModel
    {
        public string PaymentProvider { get; set; }

        public string Asset { get; set; }

        public ulong Amount { get; set; }

        public string Destination { get; set; }

        public long Fee { get; set; }
    }
}
