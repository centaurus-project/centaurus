using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain.Models
{
    public class WithdrawalWrapper
    {
        public WithdrawalWrapper(MessageEnvelope envelope, AccountWrapper accountWrapper, byte[] txHash, long maxTime)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            AccountWrapper = accountWrapper ?? throw new ArgumentNullException(nameof(envelope));
            Hash = txHash ?? throw new ArgumentNullException(nameof(txHash));
            MaxTime = maxTime;
        }

        public MessageEnvelope Envelope { get; }

        public long Apex => ((Quantum)Envelope.Message).Apex;

        public byte[] Hash { get; }

        public AccountWrapper AccountWrapper { get; }

        public List<WithdrawalWrapperItem> Items { get; set; }

        public List<DecoratedSignature> Signatures { get; set; }

        public long MaxTime { get; }

        public bool IsExpired(long currentTime, long maxTxSubmitDelay)
        {
            return currentTime - MaxTime > maxTxSubmitDelay;
        }
    }

    public class WithdrawalWrapperItem
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public RawPubKey Destination { get; set; }
    }
}