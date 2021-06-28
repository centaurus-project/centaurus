using System;
using System.Collections.Generic;
using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.Xdr;

namespace Centaurus.Domain
{
    public class Snapshot
    {
        public long Apex { get; set; }

        public byte[] LastHash { get; set; }

        public ConstellationSettings Settings { get; set; }

        public List<AccountWrapper> Accounts { get; set; }

        public List<OrderWrapper> Orders { get; set; }

        public Dictionary<string, WithdrawalStorage> Withdrawals { get; set; }
    }
}