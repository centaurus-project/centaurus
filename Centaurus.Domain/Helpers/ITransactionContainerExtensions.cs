using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class ITransactionContainerExtensions
    {
        public static stellar_dotnet_sdk.Transaction DeserializeTransaction(this ITransactionContainer withdrawalRequest)
        {
            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));

            var inputStream = new XdrDataInputStream(withdrawalRequest.TransactionXdr);
            var txXdr = Transaction.Decode(inputStream);

            //there is no methods to convert stellar_dotnet_sdk.xdr.Transaction to stellar_dotnet_sdk.Transaction, so we need wrap it first
            var txXdrEnvelope = new TransactionV1Envelope { Tx = txXdr, Signatures = new DecoratedSignature[] { } };

            return stellar_dotnet_sdk.Transaction.FromEnvelopeXdrV1(txXdrEnvelope);
        }
    }
}
