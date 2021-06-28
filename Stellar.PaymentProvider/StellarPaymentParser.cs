using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.Stellar.PaymentProvider;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class StellarPaymentParser : PaymentParserBase
    {
        public override WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, AccountWrapper account, TransactionWrapper transactionWrapper, ProviderSettings providerSettings)
        {
            var transaction = (Transaction)transactionWrapper.Transaction;
            var withdrawal = new WithdrawalWrapper(envelope, account, transactionWrapper.Hash, transactionWrapper.MaxTime)
            {
                Items = providerSettings.GetWithdrawals(transaction)
            };
            return withdrawal;
        }

        public override int CompareCursors(string left, string right)
        {
            if (!(long.TryParse(left, out var leftLong) && long.TryParse(right, out var rightLong)))
                throw new Exception("Unable to convert cursor to long.");
            return Comparer<long>.Default.Compare(leftLong, rightLong);
        }

        public override bool TryDeserializeTransaction(byte[] rawTransaction, out TransactionWrapper transaction)
        {
            transaction = null;
            try
            {
                if (rawTransaction == null)
                    throw new ArgumentNullException(nameof(rawTransaction));

                var inputStream = new stellar_dotnet_sdk.xdr.XdrDataInputStream(rawTransaction);
                var txXdr = stellar_dotnet_sdk.xdr.Transaction.Decode(inputStream);

                //there is no methods to convert stellar_dotnet_sdk.xdr.Transaction to stellar_dotnet_sdk.Transaction, so we need wrap it first
                var txXdrEnvelope = new stellar_dotnet_sdk.xdr.TransactionV1Envelope { Tx = txXdr, Signatures = new stellar_dotnet_sdk.xdr.DecoratedSignature[] { } };

                var tx = Transaction.FromEnvelopeXdrV1(txXdrEnvelope);
                transaction = new TransactionWrapper { Transaction = tx, Hash = tx.Hash(), MaxTime = tx.TimeBounds?.MaxTime ?? 0 };
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
