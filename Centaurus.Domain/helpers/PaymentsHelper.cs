using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static stellar_dotnet_sdk.xdr.OperationType;

namespace Centaurus.Domain
{
    public static class PaymentsHelper
    {
        private static bool TryGetAsset(this ExecutionContext context, Asset xdrAsset, out int asset)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var assetAlphaNum = stellar_dotnet_sdk.Asset.FromXdr(xdrAsset) as stellar_dotnet_sdk.AssetTypeCreditAlphaNum;

            asset = 0;
            if (xdrAsset.Discriminant.InnerValue == AssetType.AssetTypeEnum.ASSET_TYPE_NATIVE)
                return true;

            string assetSymbol = $"{assetAlphaNum.Code}-{assetAlphaNum.Issuer}";

            var assetSettings = context.Constellation.Assets.Find(a => a.ToString() == assetSymbol);
            if (assetSettings == null) return false;
            asset = assetSettings.Id;
            return true;
        }

        public static OperationTypeEnum[] SupportedDepositOperations = new OperationTypeEnum[] { OperationTypeEnum.PAYMENT };

        public static bool TryGetPayment(this ExecutionContext context, Operation.OperationBody operation, stellar_dotnet_sdk.KeyPair source, PaymentResults pResult, byte[] transactionHash, out PaymentBase payment)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            payment = null;
            int asset;
            //check supported deposit operations is overkill, but we need to keep SupportedDepositOperations up to date
            bool result = false;
            if (!SupportedDepositOperations.Contains(operation.Discriminant.InnerValue))
                return false;
            switch (operation.Discriminant.InnerValue)
            {
                case OperationTypeEnum.PAYMENT:
                    if (!context.TryGetAsset(operation.PaymentOp.Asset, out asset))
                        return result;
                    var amount = operation.PaymentOp.Amount.InnerValue;
                    var destKeypair = stellar_dotnet_sdk.KeyPair.FromPublicKey(operation.PaymentOp.Destination.Ed25519.InnerValue);
                    if (context.Constellation.Vault.Equals((RawPubKey)destKeypair.PublicKey))
                        payment = new Deposit
                        {
                            Destination = new RawPubKey() { Data = source.PublicKey },
                            Amount = amount,
                            Asset = asset,
                            TransactionHash = transactionHash
                        };
                    else if (context.Constellation.Vault.Equals((RawPubKey)source.PublicKey))
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
