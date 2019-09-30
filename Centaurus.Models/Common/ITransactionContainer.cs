using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public interface ITransactionContainer
    {
        public byte[] TransactionXdr { get; set; }

        public byte[] TransactionHash { get; set; }

        //TODO: move to extensions
        public stellar_dotnet_sdk.Transaction GetTransaction()
        {
            var txEnvelope = TransactionEnvelope.Decode(new XdrDataInputStream(TransactionXdr));
            return stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(txEnvelope);
        }
    }
}
