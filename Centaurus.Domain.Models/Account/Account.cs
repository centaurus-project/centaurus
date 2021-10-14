using Centaurus.Models;
using System;
using System.Collections.Generic;

namespace Centaurus.Domain.Models
{
    public class Account
    {
        public Account(RequestRateLimits requestRateLimits)
        {
            RequestCounter = new AccountRequestCounter(requestRateLimits);
        }

        public RawPubKey Pubkey { get; set; }

        public ulong Nonce { get; set; }

        public ulong AccountSequence { get; set; }

        public Dictionary<string, Balance> Balances { get; set; }

        public Dictionary<ulong, Order> Orders { get; set; }

        public RequestRateLimits RequestRateLimits { get; set; }

        public AccountRequestCounter RequestCounter { get; }
    }
}
