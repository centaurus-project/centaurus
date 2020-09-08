using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class Withdrawal
    {
        public long Apex { get; set; }

        public byte[] Hash { get; set; }

        public AccountWrapper Source { get; set; }

        public List<WithdrawalItem> Withdrawals { get; set; }

        public List<DecoratedSignature> Signatures { get; set; }

        public long MaxTime { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentTime">Time in unix time seconds</param>
        /// <returns></returns>
        public bool IsExpired(long currentTime)
        {
            return currentTime - MaxTime > Global.MaxTxSubmitDelay;
        }
    }

    public class WithdrawalItem
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public RawPubKey Destination { get; set; }
    }
}