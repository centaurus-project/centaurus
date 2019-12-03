using Centaurus.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    /// <summary>
    /// Initializes the application. 
    /// It can only be called from the Alpha, and only when it is in the waiting for initialization state.
    /// </summary>
    public class ConstellationInitializer
    {
        const int minAuditorsCount = 1;

        public ConstellationInitializer(IEnumerable<KeyPair> auditors, long minAccountBalance, long minAllowedLotSize, IEnumerable<AssetSettings> assets)
        {
            Auditors = auditors.Count() >= minAuditorsCount ? auditors.ToArray() : throw new Exception($"Min auditors count is {minAuditorsCount}");
            
            MinAccountBalance = minAccountBalance > 0 ? minAccountBalance : throw new ArgumentException("Minimal account balance is less then 0");

            MinAllowedLotSize = minAllowedLotSize > 0 ? minAllowedLotSize : throw new ArgumentException("Minimal allowed lot size is less then 0");

            Assets = !assets.GroupBy(a => a.ToString()).Any(g => g.Count() > 1) 
                ? assets.Where(a => !a.IsXlm).ToArray() //skip XLM, it's supported by default
                : throw new ArgumentException("All asset values should be unique");
        }

        public KeyPair[] Auditors { get; }
        public long MinAccountBalance { get; }
        public long MinAllowedLotSize { get; }
        public AssetSettings[] Assets { get; }

        public async Task Init()
        {
            if (Global.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("Alpha is not in the waiting for initialization state.");

            if (!await DoesAlphaAccountExist())
                throw new InvalidOperationException($"The vault ({Global.Settings.KeyPair.AccountId}) is not yet funded");

            var ledgerId = await BuildAndConfigureVault();

            SetIdToAssets();
            //build genesis settings
            var settings = new ConstellationSettings
            {
                Vault = Global.Settings.KeyPair.PublicKey,
                Auditors = Auditors.Select(key => (RawPubKey)key.PublicKey).ToList(),
                MinAccountBalance = MinAccountBalance,
                MinAllowedLotSize = MinAllowedLotSize,
                Assets = Assets.ToList()
            };

            var vaultAccountInfo = await Global.StellarNetwork.Server.Accounts.Account(Global.Settings.KeyPair.AccountId);

            //build genesis snapshot
            var snapshot = await SnapshotManager.BuildGenesisSnapshot(settings, ledgerId, vaultAccountInfo.SequenceNumber);

            Global.Setup(snapshot);

            Global.QuantumHandler.Start();
        }

        private void SetIdToAssets()
        {
            //start from 1, 0 is reserved by XLM
            for (var i = 1; i <= Assets.Length; i++)
            {
                Assets[i - 1].Id = i;
            }
        }

        /// <summary>
        /// Builds and configures Centaurus vault
        /// </summary>
        /// <returns>Ledger id</returns>
        private async Task<long> BuildAndConfigureVault()
        {
            var majority = MajorityHelper.GetMajorityCount(Auditors.Count());

            var sourceAccount = await Global.StellarNetwork.Server.Accounts.Account(Global.Settings.KeyPair.AccountId);

            var transactionBuilder = new Transaction.Builder(sourceAccount);
            transactionBuilder.SetFee(10_000);

            foreach (var a in Assets)
            {
                if (a.IsXlm) continue;
                var asset = a.ToAsset();

                var trustOperation = new ChangeTrustOperation.Builder(asset, "922337203685.4775807");
                transactionBuilder.AddOperation(trustOperation.Build());
            }

            var optionOperationBuilder = new SetOptionsOperation.Builder()
                    .SetMasterKeyWeight(0)
                    .SetLowThreshold(majority)
                    .SetMediumThreshold(majority)
                    .SetHighThreshold(majority);

            foreach (var signer in Auditors)
            {
                optionOperationBuilder.SetSigner(Signer.Ed25519PublicKey(signer), 1);
            }

            transactionBuilder.AddOperation(optionOperationBuilder.Build());

            var transaction = transactionBuilder.Build();
            transaction.Sign(Global.Settings.KeyPair);

            var result = await Global.StellarNetwork.Server.SubmitTransaction(transaction);

            if (!result.IsSuccess())
            {
                throw new Exception($"Transaction failed. Result Xdr: {result.ResultXdr}");
            }

            return result.Ledger.Value;
        }

        private async Task<bool> DoesAlphaAccountExist()
        {
            try
            {
                await Global.StellarNetwork.Server.Accounts.Account(Global.Settings.KeyPair.AccountId);
                return true;
            }
            catch (HttpResponseException exc)
            {
                if (exc.StatusCode == 404)
                    return false;
                throw;
            }
        }
    }
}
