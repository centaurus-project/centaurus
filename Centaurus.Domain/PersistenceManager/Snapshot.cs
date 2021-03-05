using System;
using System.Collections.Generic;
using Centaurus.Models;
using Centaurus.Xdr;

namespace Centaurus.Domain
{
    public class Snapshot
    {
        public long Apex { get; set; }

        public byte[] LastHash { get; set; }

        public ConstellationSettings Settings { get; set; }

        public long TxCursor { get; set; }

        public List<AccountWrapper> Accounts { get; set; }

        public List<Order> Orders { get; set; }

        public List<WithdrawalWrapper> Withdrawals { get; set; }
    }
}