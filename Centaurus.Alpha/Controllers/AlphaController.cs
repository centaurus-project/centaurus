using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Mvc;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.requests;

namespace Centaurus.Alpha.Controllers
{
    [Route("api/[controller]")]
    public class AlphaController : Controller
    {
        [HttpGet("[action]")]
        public object Info()
        {
            object info;
            if (((int)Global.AppState.State) < (int)ApplicationState.Running)
                info = new
                {
                    Global.AppState.State
                };
            else
                info = new
                {
                    Global.AppState.State,
                    Vault = ((KeyPair)Global.Constellation.Vault).AccountId,
                    Auditors = Global.Constellation.Auditors.Select(a => ((KeyPair)a).AccountId),
                    Global.Constellation.MinAccountBalance,
                    Global.Constellation.MinAllowedLotSize,
                    Global.Settings.StellarNetwork,
                    Assets = Global.Constellation.Assets.Select(a => new { a.Id, a.Code, Issuer = (a.IsXlm ? null : ((KeyPair)a.Issuer).AccountId) })
                };

            return new JsonResult(info);
        }

        //TODO: refactor this monstro-function
        [HttpPost("[action]")]
        public async Task<JsonResult> Init([FromBody] AlphaInitModel alphaInit)
        {
            try
            {

                if (Global.AppState.State != ApplicationState.WaitingForInit)
                    throw new InvalidOperationException("Alpha is already initialized");

                var minAuditorsCount = 5;

#if DEBUG
                minAuditorsCount = 1;
#endif
                ValidateInitRequest(alphaInit, minAuditorsCount);

                //cast assets to Centaurus assets 
                var assetSettings = alphaInit.Assets.Select(a => ToAssetSettings(a.Key, a.Value));

                var ledgerId = await BuildAndConfigureVault(alphaInit, assetSettings);

                //build genesis settings
                var settings = new ConstellationSettings
                {
                    Vault = Global.Settings.KeyPair.PublicKey,
                    Auditors = alphaInit.Auditors.Select(key => (RawPubKey)KeyPair.FromAccountId(key).PublicKey).ToList(),
                    MinAccountBalance = alphaInit.MinAccountBalance,
                    MinAllowedLotSize = alphaInit.MinAllowedLotSize,
                    Assets = assetSettings.ToList()
                };

                var vaultAccountInfo = await Global.StellarNetwork.Server.Accounts.Account(Global.Settings.KeyPair.AccountId);

                //build genesis snapshot
                var snapshot = await SnapshotManager.BuildGenesisSnapshot(settings, ledgerId, vaultAccountInfo.SequenceNumber);

                Global.Setup(snapshot, new MessageEnvelope[] { });

                return new JsonResult(new { IsSuccess = true });
            }
            catch (Exception exc)
            {
                return new JsonResult(new { IsSuccess = false, Error = exc.Message });
            }
        }

        /// <summary>
        /// Builds and configures Centaurus vault
        /// </summary>
        /// <param name="alphaInit"></param>
        /// <param name="assetSettings"></param>
        /// <returns>Ledger id</returns>
        private async Task<long> BuildAndConfigureVault(AlphaInitModel alphaInit, IEnumerable<AssetSettings> assetSettings)
        {

            var sourceKeyPair = KeyPair.FromSecretSeed(alphaInit.DonorSecret);

            var signers = alphaInit.Auditors.Select(a => KeyPair.FromAccountId(a));

            var majority = MajorityHelper.GetMajorityCount(alphaInit.Auditors.Length);

            var sourceAccount = await Global.StellarNetwork.Server.Accounts.Account(sourceKeyPair.AccountId);

            var transactionBuilder = new Transaction.Builder(sourceAccount);
            transactionBuilder.SetFee(10_000);
            if (!await DoesAlphaAccountExist())
                transactionBuilder.AddOperation(
                    new CreateAccountOperation.Builder(Global.Settings.KeyPair, 10.ToString()).Build()
                );

            foreach (var a in assetSettings)
            {
                if (a.IsXlm) continue;
                var asset = a.ToAsset();

                var trustOperation = new ChangeTrustOperation.Builder(asset, "922337203685.4775807");
                trustOperation.SetSourceAccount(Global.Settings.KeyPair);
                transactionBuilder.AddOperation(trustOperation.Build());
            }

            var optionOperationBuilder = new SetOptionsOperation.Builder()
                    .SetSourceAccount(Global.Settings.KeyPair)
                    .SetMasterKeyWeight(0)
                    .SetLowThreshold(majority)
                    .SetMediumThreshold(majority)
                    .SetHighThreshold(majority);

            foreach (var signer in signers)
            {
                optionOperationBuilder.SetSigner(Signer.Ed25519PublicKey(signer), 1);
            }

            optionOperationBuilder.SetSourceAccount(Global.Settings.KeyPair);

            transactionBuilder.AddOperation(optionOperationBuilder.Build());

            //TODO: fix it. Now it is failing with accountMergeHasSubEntry error
            transactionBuilder.AddOperation(new AccountMergeOperation.Builder(Global.Settings.KeyPair).Build());

            var transaction = transactionBuilder.Build();

            transaction.Sign(sourceKeyPair);
            transaction.Sign(Global.Settings.KeyPair);

            var result = await Global.StellarNetwork.Server.SubmitTransaction(transaction);

            if (!result.IsSuccess())
            {
                throw new Exception($"Transaction failed. Result Xdr: {result.ResultXdr}");
            }

            return result.Ledger.Value;
        }

        private void ValidateInitRequest(AlphaInitModel alphaInit, int minAuditorsCount)
        {

            if (alphaInit.Auditors.Length < minAuditorsCount)
                throw new Exception("Not enough auditors");

            if (alphaInit.MinAccountBalance < 1)
                throw new ArgumentException("Minimal account balance is less then 0");

            if (alphaInit.MinAllowedLotSize < 1)
                throw new ArgumentException("Minimal allowed lot size is less then 0");

            if (alphaInit.Assets.GroupBy(v => v.Key).Any(g => g.Count() > 1))
                throw new ArgumentException("All asset values should be unique");
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

        private AssetSettings ToAssetSettings(string code, int id)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentNullException(nameof(code));

            if (id < 0)
                throw new ArgumentNullException(nameof(id));

            var assetData = code.Split("-", StringSplitOptions.RemoveEmptyEntries);
            if (assetData.Length != 1 && assetData.Length != 3) //if length is 1 then it's a native asset, else it should have code, asset type and issuer
                throw new Exception("Unable to parse asset");

            return new AssetSettings { Code = assetData[0], Issuer = assetData.Length > 1 ? new RawPubKey(assetData[1]) : null, Id = id };
        }
    }

    public class AlphaInitModel
    {
        public string DonorSecret { get; set; }

        public string[] Auditors { get; set; }

        public int MinAccountBalance { get; set; }

        public int MinAllowedLotSize { get; set; }

        public Dictionary<string, int> Assets { get; set; }
    }
}