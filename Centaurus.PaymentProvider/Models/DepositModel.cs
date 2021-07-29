using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider.Models
{
    public class DepositModel
    {
        public bool IsSuccess { get; set; }

        public byte[] TransactionHash { get; set; }

        public string Asset { get; set; }

        public ulong Amount { get; set; }

        public byte[] Destination { get; set; }
    }
}
