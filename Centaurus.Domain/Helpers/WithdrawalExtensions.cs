using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using stellar_dotnet_sdk.xdr;
using System.Linq;

namespace Centaurus.Domain
{
    public static class WithdrawalExtensions
    {
        public static List<WithdrawalWrapperItem> GetWithdrawals(this stellar_dotnet_sdk.Transaction transaction, Account sourceAccount, ConstellationSettings constellationSettings)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            if (sourceAccount == null)
                throw new ArgumentNullException(nameof(sourceAccount));

            var payments = transaction.Operations
                .Where(o => o is stellar_dotnet_sdk.PaymentOperation)
                .Cast<stellar_dotnet_sdk.PaymentOperation>();

            var withdrawals = new List<WithdrawalWrapperItem>();
            foreach (var payment in payments)
            {
                if (!constellationSettings.TryFindAssetSettings(payment.Asset, out var asset))
                    throw new BadRequestException("Asset is not allowed by constellation.");

                if (!ByteArrayPrimitives.Equals(payment.SourceAccount?.PublicKey, constellationSettings.Vault.Data))
                    throw new BadRequestException("Only vault account can be used as payment source.");
                var amount = stellar_dotnet_sdk.Amount.ToXdr(payment.Amount);
                if (amount < constellationSettings.MinAllowedLotSize)
                    throw new BadRequestException($"Min withdrawal amount is {constellationSettings.MinAllowedLotSize} stroops.");
                if (!(sourceAccount.GetBalance(asset.Id)?.HasSufficientBalance(amount) ?? false))
                    throw new BadRequestException($"Insufficient balance.");

                withdrawals.Add(new WithdrawalWrapperItem
                {
                    Asset = asset.Id,
                    Amount = amount,
                    Destination = payment.Destination.PublicKey
                });
            }
            if (withdrawals.GroupBy(w => w.Asset).Any(g => g.Count() > 1))
                throw new BadRequestException("Multiple payments for the same asset.");

            return withdrawals;
        }
    }
}
