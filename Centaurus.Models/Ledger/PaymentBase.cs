using stellar_dotnet_sdk.xdr;
using System;
using static stellar_dotnet_sdk.xdr.OperationType;

namespace Centaurus.Models
{
    public abstract class PaymentBase: IXdrSerializableModel
    {
        public abstract PaymentTypes Type { get; }

        public int Asset { get; set; }

        public long Amount { get; set; }

        public RawPubKey Destination { get; set; }

        public byte[] TransactionHash { get; set; }

        public PaymentResults PaymentResult { get; set; }
    }
}
