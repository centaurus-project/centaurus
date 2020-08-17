using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static stellar_dotnet_sdk.xdr.OperationType;

namespace Centaurus.Domain
{
    public static class PaymentsHelper
    {
        private static bool TryGetAsset(Asset xdrAsset, out int asset)
        {
            var assetAlphaNum = stellar_dotnet_sdk.Asset.FromXdr(xdrAsset) as stellar_dotnet_sdk.AssetTypeCreditAlphaNum;

            asset = 0;
            if (xdrAsset.Discriminant.InnerValue == AssetType.AssetTypeEnum.ASSET_TYPE_NATIVE)
                return true;

            string assetSymbol = $"{assetAlphaNum.Code}-{assetAlphaNum.Issuer}";

            var assetSettings = Global.Constellation.Assets.Find(a => a.ToString() == assetSymbol);
            if (assetSettings == null) return false;
            asset = assetSettings.Id;
            return true;
        }

        public static bool FromOperationResponse(Operation.OperationBody operation, stellar_dotnet_sdk.KeyPair source, PaymentResults pResult, byte[] transactionHash, out PaymentBase payment)
        {
            payment = null;
            int asset;
            bool result = false;
            switch (operation.Discriminant.InnerValue)
            {
                case OperationTypeEnum.PAYMENT:
                    if (!TryGetAsset(operation.PaymentOp.Asset, out asset))
                        return result;
                    var amount = operation.PaymentOp.Amount.InnerValue;
                    var destKeypair = stellar_dotnet_sdk.KeyPair.FromPublicKey(operation.PaymentOp.Destination.Ed25519.InnerValue);
                    if (Global.Constellation.Vault.Equals((RawPubKey)destKeypair.PublicKey))
                        payment = new Deposit
                        {
                            Destination = new RawPubKey() { Data = source.PublicKey },
                            Amount = amount,
                            Asset = asset,
                            TransactionHash = transactionHash
                        };
                    else if (Global.Constellation.Vault.Equals((RawPubKey)source.PublicKey))
                    {
                        var withdrawal = Global.WithdrawalStorage.GetWithdrawal(transactionHash);
                        if (withdrawal == null)
                            throw new Exception("Unable to find withdrawal by hash.");
                        if (withdrawal.Asset != asset)
                            throw new Exception("Assets are not equal.");
                        if (withdrawal.Amount != amount)
                            throw new Exception("Amounts are not equal.");
                        if (ByteArrayPrimitives.Equals(withdrawal.Destination, destKeypair))
                            throw new Exception("Destinations are not equal.");
                        payment = withdrawal;
                    }
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

        public static stellar_dotnet_sdk.Transaction GenerateTransaction(this PaymentRequestBase withdrawalRequest)
        {
            return withdrawalRequest.GenerateTransaction(Global.Constellation.Assets, Global.VaultAccount.GetAccount());
        }

        public static stellar_dotnet_sdk.Transaction GenerateTransaction(this PaymentRequestBase withdrawalRequest, List<AssetSettings> assets, stellar_dotnet_sdk.Account account)
        {
            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));
            if (assets == null)
                throw new ArgumentNullException(nameof(assets));
            if (account == null)
                throw new ArgumentNullException(nameof(account));
            stellar_dotnet_sdk.Asset asset = new stellar_dotnet_sdk.AssetTypeNative();
            if (withdrawalRequest.Asset != 0)
                asset = assets.Find(a => a.Id == withdrawalRequest.Asset).ToAsset();

            var transaction = TransactionHelper.BuildPaymentTransaction(
                new TransactionBuilderOptions(account, 10_000/*TODO: move fee to settings*/, withdrawalRequest.Memo),
                new stellar_dotnet_sdk.KeyPair(withdrawalRequest.Destination.ToArray()),
                asset,
                withdrawalRequest.Amount
            );

            return transaction;
        }
    }
}
