using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using stellar_dotnet_sdk.xdr;

namespace Centaurus.Domain
{
    public static class ITransactionContainerExtensions
    {
        public static stellar_dotnet_sdk.Transaction GenerateTransaction(this ITransactionContainer transactionContainer)
        {
            if (transactionContainer == null)
                throw new ArgumentNullException(nameof(transactionContainer));
            if (transactionContainer is PaymentRequestBase)
                return ((PaymentRequestBase)transactionContainer).GenerateTransaction();
            throw new Exception($"Unable to generate transaction for {transactionContainer.GetType().Name}");
        }

        public static void AssignTransactionXdr(this ITransactionContainer transactionContainer, stellar_dotnet_sdk.Transaction transaction)
        {
            if (transactionContainer == null)
                throw new ArgumentNullException(nameof(transactionContainer));
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            var outputStream = new XdrDataOutputStream();
            var txXdr = transaction.ToXdrV1();
            Transaction.Encode(outputStream, txXdr);

            transactionContainer.TransactionXdr = outputStream.ToArray();
            transactionContainer.TransactionHash = transaction.Hash();
        }

        public static bool HasTransaction(this ITransactionContainer transactionContainer)
        {
            if (transactionContainer == null)
                throw new ArgumentNullException(nameof(transactionContainer));
            return transactionContainer.TransactionHash != null
                && transactionContainer.TransactionHash.Length > 0;
        }

        public static stellar_dotnet_sdk.Transaction DeserializeTransaction(this ITransactionContainer transactionContainer)
        {
            if (transactionContainer == null)
                throw new ArgumentNullException(nameof(transactionContainer));
            if (!transactionContainer.HasTransaction())
                throw new Exception("Transaction container doesn't have transaction xdr.");

            var inputStream = new XdrDataInputStream(transactionContainer.TransactionXdr);
            var txXdr = Transaction.Decode(inputStream);

            //there is no methods to convert stellar_dotnet_sdk.xdr.Transaction to stellar_dotnet_sdk.Transaction, so we need wrap it first
            var txXdrEnvelope = new TransactionV1Envelope { Tx = txXdr, Signatures = new DecoratedSignature[] { } };

            return stellar_dotnet_sdk.Transaction.FromEnvelopeXdrV1(txXdrEnvelope);
        }
    }
}
