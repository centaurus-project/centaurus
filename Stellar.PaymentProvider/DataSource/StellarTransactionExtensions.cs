using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using stellar_dotnet_sdk.xdr;
using System.Linq;
using static stellar_dotnet_sdk.xdr.OperationType;
using Centaurus.Domain.Models;

namespace Centaurus.Stellar.PaymentProvider
{
    public static class StellarTransactionExtensions
    {
        public static List<WithdrawalWrapperItem> GetWithdrawals(this ProviderSettings providerSettings, stellar_dotnet_sdk.Transaction transaction)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            var payments = transaction.Operations
                .Where(o => o is stellar_dotnet_sdk.PaymentOperation)
                .Cast<stellar_dotnet_sdk.PaymentOperation>();

            var withdrawals = new List<WithdrawalWrapperItem>();
            foreach (var payment in payments)
            {
                if (providerSettings.TryGetAsset(payment.Asset, out var asset))
                    throw new BadRequestException("Asset is not allowed by constellation.");

                if (payment.SourceAccount?.AccountId != providerSettings.Vault)
                    throw new BadRequestException("Only vault account can be used as payment source.");

                var amount = stellar_dotnet_sdk.Amount.ToXdr(payment.Amount);
                withdrawals.Add(new WithdrawalWrapperItem
                {
                    Asset = asset.CentaurusAsset,
                    Amount = amount,
                    Destination = payment.Destination.PublicKey
                });
            }
            if (withdrawals.GroupBy(w => w.Asset).Any(g => g.Count() > 1))
                throw new BadRequestException("Multiple payments for the same asset.");

            return withdrawals;
        }

        private static bool TryGetAsset(this ProviderSettings providerSettings, Asset xdrAsset, out ProviderAsset asset)
        {
            var sdkAsset = stellar_dotnet_sdk.Asset.FromXdr(xdrAsset);
            return providerSettings.TryGetAsset(sdkAsset, out asset);
        }

        private static bool TryGetAsset(this ProviderSettings providerSettings, stellar_dotnet_sdk.Asset xdrAsset, out ProviderAsset asset)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            var name = xdrAsset is stellar_dotnet_sdk.AssetTypeCreditAlphaNum creditAsset ? creditAsset.CanonicalName() : "XLM";
            asset = providerSettings.Assets.FirstOrDefault(a => a.Token == name);
            return asset != null;
        }

        public static OperationTypeEnum[] SupportedDepositOperations = new OperationTypeEnum[] { OperationTypeEnum.PAYMENT };

        public static bool TryGetPayment(this ProviderSettings providerSettings, Operation.OperationBody operation, stellar_dotnet_sdk.KeyPair source, PaymentResults pResult, byte[] transactionHash, out PaymentBase payment)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            var vault = stellar_dotnet_sdk.KeyPair.FromAccountId(providerSettings.Vault);

            payment = null;
            ProviderAsset asset;
            //check supported deposit operations is overkill, but we need to keep SupportedDepositOperations up to date
            bool result = false;
            if (!SupportedDepositOperations.Contains(operation.Discriminant.InnerValue))
                return false;
            switch (operation.Discriminant.InnerValue)
            {
                case OperationTypeEnum.PAYMENT:
                    if (!providerSettings.TryGetAsset(operation.PaymentOp.Asset, out asset))
                        return result;
                    var amount = operation.PaymentOp.Amount.InnerValue;
                    var destKeypair = stellar_dotnet_sdk.KeyPair.FromPublicKey(operation.PaymentOp.Destination.Ed25519.InnerValue);
                    if (vault.Equals((RawPubKey)destKeypair.PublicKey))
                        payment = new Deposit
                        {
                            Destination = new RawPubKey() { Data = source.PublicKey },
                            Amount = amount,
                            Asset = asset.CentaurusAsset,
                            TransactionHash = transactionHash
                        };
                    else if (vault.Equals((RawPubKey)source.PublicKey))
                        payment = new Withdrawal { TransactionHash = transactionHash };
                    if (payment != null)
                    {
                        payment.PaymentResult = pResult;
                        result = true;
                    }
                    break;
                case OperationTypeEnum.PATH_PAYMENT_STRICT_SEND:
                case OperationTypeEnum.PATH_PAYMENT_STRICT_RECEIVE:
                    //TODO: handle path payment
                    break;
                case OperationTypeEnum.ACCOUNT_MERGE:
                    //TODO: handle account merge
                    break;
            }
            return result;
        }
    }
}
