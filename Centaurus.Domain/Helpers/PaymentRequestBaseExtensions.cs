using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using stellar_dotnet_sdk.xdr;
using System.Linq;

namespace Centaurus.Domain
{
    public static class PaymentRequestBaseExtensions
    {
        public static stellar_dotnet_sdk.Transaction DeserializeTransaction(this PaymentRequestBase payment)
        {
            if (payment == null)
                throw new ArgumentNullException(nameof(payment));

            var inputStream = new XdrDataInputStream(payment.TransactionXdr);
            var txXdr = Transaction.Decode(inputStream);

            //there is no methods to convert stellar_dotnet_sdk.xdr.Transaction to stellar_dotnet_sdk.Transaction, so we need wrap it first
            var txXdrEnvelope = new TransactionV1Envelope { Tx = txXdr, Signatures = new DecoratedSignature[] { } };

            return stellar_dotnet_sdk.Transaction.FromEnvelopeXdrV1(txXdrEnvelope);
        }

        public static List<WithdrawalItem> GetWithdrawals(this stellar_dotnet_sdk.Transaction transaction, Account sourceAccount)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            if (sourceAccount == null)
                throw new ArgumentNullException(nameof(sourceAccount));

            var payments = transaction.Operations
                .Where(o => o is stellar_dotnet_sdk.PaymentOperation)
                .Cast<stellar_dotnet_sdk.PaymentOperation>();

            var withdrawals = new List<WithdrawalItem>();
            foreach (var payment in payments)
            {
                if (!Global.Constellation.TryFindAssetSettings(payment.Asset, out var asset))
                    throw new InvalidOperationException("Asset is not allowed by constellation.");

                if (!ByteArrayPrimitives.Equals(payment.SourceAccount?.PublicKey, Global.Constellation.Vault.Data))
                    throw new InvalidOperationException("Only vault account can be used as payment source.");
                var amount = stellar_dotnet_sdk.Amount.ToXdr(payment.Amount);
                if (amount < Global.MinWithdrawalAmount)
                    throw new InvalidOperationException($"Min withdrawal amount is {Global.MinWithdrawalAmount} stroops.");
                if (!(sourceAccount.GetBalance(asset.Id)?.HasSufficientBalance(amount) ?? false))
                    throw new InvalidOperationException($"Insufficient balance.");

                withdrawals.Add(new WithdrawalItem
                {
                    Asset = asset.Id,
                    Amount = amount,
                    Destination = payment.Destination.PublicKey
                });
            }
            if (withdrawals.GroupBy(w => w.Asset).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Multiple payments for the same asset.");

            return withdrawals;
        }

        public static Withdrawal GetWithdrawal(this PaymentRequestBase payment, long apex, AccountWrapper sourceAccount)
        {
            var transaction = payment.DeserializeTransaction();
            var transactionHash = transaction.Hash();
            var withdrawal = new Withdrawal
            {
                Apex = apex,
                Hash = transactionHash,
                Source = sourceAccount,
                Withdrawals = GetWithdrawals(transaction, sourceAccount.Account)
            };
            return withdrawal;
        }
    }
}
