using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class WithdrawalWrapper
    {
        public MessageEnvelope Envelope { get; set; }

        public long Apex => ((Quantum)Envelope.Message).Apex;

        public byte[] Hash { get; set; }

        public AccountWrapper Source => ((RequestQuantum)Envelope.Message).RequestMessage.AccountWrapper;

        public List<WithdrawalWrapperItem> Withdrawals { get; set; }

        public List<DecoratedSignature> Signatures { get; set; }

        public long MaxTime { get; set; }
    }

    public class WithdrawalWrapperItem
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public RawPubKey Destination { get; set; }
    }


}