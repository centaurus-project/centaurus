using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain.Transactions.Parsers
{
    public class StellarPaymentParser : PaymentsParserBase<Transaction>
    {
        public override PaymentProvider Provider => PaymentProvider.Stellar;
        public override WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, TransactionWrapper<Transaction> transactionWrapper, ConstellationSettings constellationSettings, string vault)
        {
            var withdrawalRequest = (WithdrawalRequest)((RequestQuantum)envelope.Message).RequestMessage;
            var withdrawal = new WithdrawalWrapper(envelope, transactionWrapper.Hash, transactionWrapper.MaxTime)
            {
                Items = transactionWrapper.Transaction.GetWithdrawals(withdrawalRequest.AccountWrapper.Account, constellationSettings, vault)
            };
            return withdrawal;
        }

        public override int CompareCursors(string left, string right)
        {
            if (!(long.TryParse(left, out var leftLong) && long.TryParse(right, out var rightLong)))
                throw new Exception("Unable to convert cursor to long.");
            return Comparer<long>.Default.Compare(leftLong, rightLong);
        }


        public override object ParseCursor(string cursor)
        {
            if (!long.TryParse(cursor, out var cursorLong))
                throw new Exception("Unable to convert cursor to long.");
            return cursorLong;
        }

        public override object ParseSecret(string secret)
        {
            return KeyPair.FromSecretSeed(secret);
        }

        public override object ParseVault(string vault)
        {
            return KeyPair.FromAccountId(vault);
        }

        public override bool TryDeserializeTransaction(byte[] rawTransaction, out TransactionWrapper<Transaction> transaction)
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
                transaction = new TransactionWrapper<Transaction> { Transaction = tx, Hash = tx.Hash(), MaxTime = tx.TimeBounds?.MaxTime ?? 0 };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override void ValidateTransaction(TransactionWrapper<Transaction> transactionWrapper, string vault)
        {
            var transaction = transactionWrapper.Transaction;
            var txSourceAccount = transaction.SourceAccount;
            if (vault == txSourceAccount.AccountId)
                throw new BadRequestException("Vault account cannot be used as transaction source.");

            if (transaction.TimeBounds == null || transaction.TimeBounds.MaxTime <= 0)
                throw new BadRequestException("Max time must be set.");

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (transaction.TimeBounds.MaxTime - currentTime > 1000)
                throw new BadRequestException("Transaction expiration time is to far.");

            if (transaction.Operations.Any(o => !(o is PaymentOperation)))
                throw new BadRequestException("Only payment operations are allowed.");

            if (transaction.Operations.Length > 100)
                throw new BadRequestException("Too many operations.");
        }
    }
}
