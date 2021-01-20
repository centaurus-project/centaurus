using Centaurus.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.requests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ConstellationInitInfo
    {
        public KeyPair[] Auditors { get; set; }
        public long MinAccountBalance { get; set; }
        public long MinAllowedLotSize { get; set; }
        public AssetSettings[] Assets { get; set; }
        public RequestRateLimits RequestRateLimits { get; set; }
    }

    /// <summary>
    /// Initializes the application. 
    /// It can only be called from the Alpha, and only when it is in the waiting for initialization state.
    /// </summary>
    public class ConstellationInitializer
    {
        const int minAuditorsCount = 1;

        ConstellationInitInfo constellationInitInfo { get; }

        public ConstellationInitializer(ConstellationInitInfo constellationInitInfo)
        {
            if (constellationInitInfo.Auditors == null || constellationInitInfo.Auditors.Count() < minAuditorsCount)
                throw new ArgumentException($"Min auditors count is {minAuditorsCount}");

            if (constellationInitInfo.MinAccountBalance < 1)
                throw new ArgumentException("Minimal account balance is less then 0");

            if (constellationInitInfo.MinAllowedLotSize < 1)
                throw new ArgumentException("Minimal allowed lot size is less then 0");

            if (constellationInitInfo.Assets.GroupBy(a => a.ToString()).Any(g => g.Count() > 1))
                throw new ArgumentException("All asset values should be unique");

            if (constellationInitInfo.Assets.Any(a => a.IsXlm))
                throw new ArgumentException("Specify only custom assets. Native assets are supported by default.");

            if (constellationInitInfo.RequestRateLimits == null || constellationInitInfo.RequestRateLimits.HourLimit < 1 || constellationInitInfo.RequestRateLimits.MinuteLimit < 1)
                throw new ArgumentException("Request rate limit values should be greater than 0");

            this.constellationInitInfo = constellationInitInfo;
        }

        public async Task Init()
        {
            if (Global.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("Alpha is not in the waiting for initialization state.");

            var alphaAccountData = await DoesAlphaAccountExist();
            if (alphaAccountData == null)
                throw new InvalidOperationException($"The vault ({Global.Settings.KeyPair.AccountId}) is not yet funded");

            var txCursor = await BuildAndConfigureVault(alphaAccountData);

            SetIdToAssets();

            var initQuantum = new ConstellationInitQuantum
            {
                Assets = constellationInitInfo.Assets.ToList(),
                Auditors = constellationInitInfo.Auditors.Select(key => (RawPubKey)key.PublicKey).ToList(),
                Vault = Global.Settings.KeyPair.PublicKey,
                MinAccountBalance = constellationInitInfo.MinAccountBalance,
                MinAllowedLotSize = constellationInitInfo.MinAllowedLotSize,
                PrevHash = new byte[] { },
                RequestRateLimits = constellationInitInfo.RequestRateLimits,
                TxCursor = txCursor
            };

            var envelope = initQuantum.CreateEnvelope();

            await Global.QuantumHandler.HandleAsync(envelope);
        }

        private void SetIdToAssets()
        {
            //start from 1, 0 is reserved by XLM
            for (var i = 1; i <= constellationInitInfo.Assets.Length; i++)
            {
                constellationInitInfo.Assets[i - 1].Id = i;
            }
        }

        /// <summary>
        /// Builds and configures Centaurus vault
        /// </summary>
        /// <returns>Transaction cursor</returns>
        private async Task<long> BuildAndConfigureVault(stellar_dotnet_sdk.responses.AccountResponse vaultAccount)
        {
            var majority = MajorityHelper.GetMajorityCount(constellationInitInfo.Auditors.Count());

            var sourceAccount = await StellarAccountHelper.GetStellarAccount(vaultAccount.KeyPair);

            var transactionBuilder = new TransactionBuilder(sourceAccount);
            transactionBuilder.SetFee(10_000);

            var existingTrustlines = vaultAccount.Balances
                .Where(b => b.Asset is stellar_dotnet_sdk.AssetTypeCreditAlphaNum)
                .Select(b => b.Asset)
                .Cast<stellar_dotnet_sdk.AssetTypeCreditAlphaNum>();
            foreach (var a in constellationInitInfo.Assets)
            {
                var asset = a.ToAsset() as stellar_dotnet_sdk.AssetTypeCreditAlphaNum;

                if (asset == null)//if null than asset is stellar_dotnet_sdk.AssetTypeNative
                    throw new InvalidOperationException("Native assets are supported by default."); //better to throw exception to avoid confusions with id

                if (existingTrustlines.Any(t => t.Code == asset.Code && t.Issuer == asset.Issuer))
                    continue;

                var trustOperation = new ChangeTrustOperation.Builder(asset, "922337203685.4775807");
                transactionBuilder.AddOperation(trustOperation.Build());
            }

            var optionOperationBuilder = new SetOptionsOperation.Builder()
                    .SetMasterKeyWeight(0)
                    .SetLowThreshold(majority)
                    .SetMediumThreshold(majority)
                    .SetHighThreshold(majority);

            transactionBuilder.AddOperation(optionOperationBuilder.Build());

            foreach (var signer in constellationInitInfo.Auditors)
            {
                transactionBuilder.AddOperation(new SetOptionsOperation.Builder().SetSigner(Signer.Ed25519PublicKey(signer), 1).Build());
            }

            var transaction = transactionBuilder.Build();
            transaction.Sign(Global.Settings.KeyPair);

            var result = await Global.StellarNetwork.Server.SubmitTransaction(transaction);

            if (!result.IsSuccess())
            {
                throw new Exception($"Transaction failed. Result Xdr: {result.ResultXdr}");
            }

            var tx = await Global.StellarNetwork.Server.Transactions.Transaction(result.Hash);
            return long.Parse(tx.PagingToken);
        }

        private async Task<stellar_dotnet_sdk.responses.AccountResponse> DoesAlphaAccountExist()
        {
            try
            {
                return await StellarAccountHelper.GetStellarAccount(Global.Settings.KeyPair);
            }
            catch (HttpResponseException exc)
            {
                if (exc.StatusCode == 404)
                    return null;
                throw;
            }
        }
    }
}
