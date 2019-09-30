using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class PaymentRequestBase : RequestMessage, ITransactionContainer
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public RawPubKey Destination { get; set; }

        public string Memo { get; set; } = string.Empty;

        public byte[] TransactionXdr { get; set; }

        public byte[] TransactionHash { get; set; }
    }
}
