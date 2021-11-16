using Centaurus.PaymentProvider.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Linq;
using static stellar_dotnet_sdk.xdr.OperationType;

namespace Centaurus.Stellar.PaymentProvider
{
    public static class StellarTransactionExtensions
    {
        public static stellar_dotnet_sdk.AssetTypeNative NativeAsset = new stellar_dotnet_sdk.AssetTypeNative();

        public static bool TryGetAsset(this SettingsModel providerSettings, Asset xdrAsset, out AssetModel asset)
        {
            var sdkAsset = stellar_dotnet_sdk.Asset.FromXdr(xdrAsset);
            return providerSettings.TryGetAsset(sdkAsset, out asset);
        }

        public static bool TryGetAssetByCanonicalName(string name, out stellar_dotnet_sdk.Asset asset)
        {
            asset = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name == NativeAsset.CanonicalName())
                asset = NativeAsset;
            else
            {
                var splitted = name.Split(':');
                if (splitted.Length != 2)
                    return false;
                asset = stellar_dotnet_sdk.Asset.CreateNonNativeAsset(splitted[0], splitted[1]);
            }
            return asset != null;
        }

        public static bool TryGetAsset(this SettingsModel providerSettings, stellar_dotnet_sdk.Asset xdrAsset, out AssetModel asset)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            asset = providerSettings.Assets.FirstOrDefault(a => a.Token == xdrAsset.CanonicalName());
            return asset != null;
        }

        public static bool TryGetAsset(this SettingsModel providerSettings, string asset, out stellar_dotnet_sdk.Asset stellarAsset)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            var providerAsset = providerSettings.Assets.FirstOrDefault(a => a.CentaurusAsset == asset);
            return TryGetAssetByCanonicalName(providerAsset.Token, out stellarAsset);
        }

        public static OperationTypeEnum[] SupportedDepositOperations = new OperationTypeEnum[] { OperationTypeEnum.PAYMENT };

        public static bool TryGetDeposit(this SettingsModel providerSettings, Operation.OperationBody operation, byte[] destination, bool isSuccess, byte[] transactionHash, out DepositModel payment)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));

            var vault = stellar_dotnet_sdk.KeyPair.FromAccountId(providerSettings.Vault);

            payment = null;
            AssetModel asset;
            //check supported deposit operations is overkill, but we need to keep SupportedDepositOperations up to date
            bool result = false;
            if (!SupportedDepositOperations.Contains(operation.Discriminant.InnerValue))
                return false;
            switch (operation.Discriminant.InnerValue)
            {
                case OperationTypeEnum.PAYMENT:
                    if (!providerSettings.TryGetAsset(operation.PaymentOp.Asset, out asset))
                        return result;
                    var amount = (ulong)operation.PaymentOp.Amount.InnerValue;
                    var destKeypair = stellar_dotnet_sdk.KeyPair.FromPublicKey(operation.PaymentOp.Destination.Ed25519.InnerValue);
                    if (vault.PublicKey.AsSpan().SequenceEqual(destKeypair.PublicKey))
                        payment = new DepositModel
                        {
                            Destination = destination,
                            Amount = amount,
                            Asset = asset.CentaurusAsset,
                            TransactionHash = transactionHash
                        };
                    if (payment != null)
                    {
                        payment.IsSuccess = isSuccess;
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

        public static bool TryDeserializeTransaction(byte[] rawTransaction, out stellar_dotnet_sdk.Transaction transaction)
        {
            transaction = null;
            try
            {
                if (rawTransaction == null)
                    throw new ArgumentNullException(nameof(rawTransaction));

                var inputStream = new XdrDataInputStream(rawTransaction);
                var txXdr = Transaction.Decode(inputStream);

                //there is no methods to convert stellar_dotnet_sdk.xdr.Transaction to stellar_dotnet_sdk.Transaction, so we need wrap it first
                var txXdrEnvelope = new TransactionV1Envelope { Tx = txXdr, Signatures = new DecoratedSignature[] { } };

                transaction = stellar_dotnet_sdk.Transaction.FromEnvelopeXdrV1(txXdrEnvelope);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
