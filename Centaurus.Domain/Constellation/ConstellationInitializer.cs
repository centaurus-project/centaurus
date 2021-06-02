using Centaurus.Models;
using Centaurus.Stellar;
using Centaurus.Stellar.Models;
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
        public List<Vault> Vaults { get; set; }
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
    public class ConstellationInitializer: ContextualBase
    {
        const int minAuditorsCount = 1;

        ConstellationInitInfo constellationInitInfo;

        public ConstellationInitializer(ConstellationInitInfo constellationInitInfo, ExecutionContext context)
            :base(context)
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
            if (Context.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("Alpha is not in the waiting for initialization state.");

            var alphaAccountData = await DoesAlphaAccountExist();
            if (alphaAccountData == null)
                throw new InvalidOperationException($"The vault ({Context.Settings.KeyPair.AccountId}) is not yet funded");

            var cursors = await BuildAndConfigureVault(alphaAccountData);

            SetIdToAssets();

            var initQuantum = new ConstellationInitRequest
            {
                Assets = constellationInitInfo.Assets.ToList(),
                Auditors = constellationInitInfo.Auditors.Select(key => (RawPubKey)key.PublicKey).ToList(),
                Vaults = constellationInitInfo.Vaults,
                MinAccountBalance = constellationInitInfo.MinAccountBalance,
                MinAllowedLotSize = constellationInitInfo.MinAllowedLotSize,
                RequestRateLimits = constellationInitInfo.RequestRateLimits,
                Cursors = cursors
            }.CreateEnvelope();

            var res = await Context.QuantumHandler.HandleAsync(initQuantum);
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
        private async Task<List<PaymentCursor>> BuildAndConfigureVault(AccountModel vaultAccount)
        {
            var majority = MajorityHelper.GetMajorityCount(constellationInitInfo.Auditors.Count());

            var sourceAccount = await Context.StellarDataProvider.GetAccountData(vaultAccount.KeyPair.AccountId);

            var transactionBuilder = new TransactionBuilder(sourceAccount.ToITransactionBuilderAccount());
            transactionBuilder.SetFee(10_000);

            foreach (var a in constellationInitInfo.Assets)
            {
                if (a.IsXlm)
                    throw new InvalidOperationException("Native assets are supported by default."); //better to throw exception to avoid confusions with id

                if (vaultAccount.ExistingTrustLines.Any(ta => ta == a.ToString()))
                    continue;

                var trustOperation = new ChangeTrustOperation.Builder(a.ToAsset(), "922337203685.4775807");
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
            transaction.Sign(Context.Settings.KeyPair);

            var result = await Context.StellarDataProvider.SubmitTransaction(transaction);

            if (!result.IsSuccess)
            {
                throw new Exception($"Transaction failed. Result Xdr: {result.ResultXdr}");
            }

            var tx = await Context.StellarDataProvider.GetTransaction(result.Hash);
            return new List<PaymentCursor> { new PaymentCursor { Provider = PaymentProvider.Stellar, Cursor = tx.PagingToken.ToString() } };
        }

        private async Task<AccountModel> DoesAlphaAccountExist()
        {
            try
            {
                return await Context.StellarDataProvider.GetAccountData(Context.Settings.KeyPair.AccountId);
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
