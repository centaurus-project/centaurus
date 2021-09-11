using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using Centaurus.Stellar.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Stellar.PaymentProvider
{
    public class StellarPaymentProvider : PaymentProviderBase
    {
        public StellarPaymentProvider(SettingsModel settings, string config)
            : base(settings, config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            var configObject = JsonSerializer.Deserialize<Config>(config);

            dataSource = new DataSource(configObject.PassPhrase, configObject.Horizon);

            secret = KeyPair.FromSecretSeed(configObject.Secret);

            //wait while all missed transactions will be loaded
            LoadLastTxs().Wait();

            Task.Factory.StartNew(ListenTransactions, TaskCreationOptions.LongRunning);
        }

        private async Task LoadLastTxs()
        {
            var pageSize = 200;
            while (true)
            {
                var txs = await dataSource.GetTransactions(Vault, long.Parse(LastRegisteredCursor), pageSize);
                foreach (var tx in txs)
                {
                    ProcessTransaction(tx);
                }
                if (txs.Count < pageSize)
                    break;
            }
        }

        public override bool IsTransactionValid(byte[] rawTransaction, WithdrawalRequestModel withdrawalRequest, out string error)
        {
            error = null;
            if (rawTransaction == null)
                throw new ArgumentNullException(nameof(rawTransaction));

            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));

            if (!rawTransaction.AsSpan().SequenceEqual(BuildTransaction(withdrawalRequest)))
            {
                error = "Transaction is not equal to expected one.";
                return false;
            }
            return true;
        }

        private bool ValidateFee(uint fee)
        {
            return true;
        }

        private Account GetSourceAccount()
        {
            return null;
        }

        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);
        private AccountModel vaultAccount;
        private async Task<AccountModel> GetVaultAccount()
        {
            if (vaultAccount != null)
                return vaultAccount;

            await syncRoot.WaitAsync();
            try
            {
                if (vaultAccount != null)
                    return vaultAccount;
                vaultAccount = await dataSource.GetAccountData(Vault);
                return vaultAccount;
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public override byte[] BuildTransaction(WithdrawalRequestModel withdrawalRequest)
        {
            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));

            if (!ValidateFee((uint)withdrawalRequest.Fee))
                throw new InvalidOperationException($"Not fair fee {withdrawalRequest.Fee}.");
            var options = new TransactionBuilderOptions(GetSourceAccount(), (uint)withdrawalRequest.Fee);
            if (!Settings.TryGetAsset(withdrawalRequest.Asset, out var stellarAsset))
                throw new InvalidOperationException($"Asset {withdrawalRequest.Asset} is not supported by provider.");

            var transaction = TransactionHelper.BuildPaymentTransaction(options, stellar_dotnet_sdk.KeyPair.FromPublicKey(withdrawalRequest.Destination), stellarAsset, (long)withdrawalRequest.Amount);
            var txSourceAccount = transaction.SourceAccount;
            if (Vault == txSourceAccount.AccountId)
                throw new InvalidOperationException("Vault account cannot be used as transaction source.");

            if (transaction.TimeBounds == null || transaction.TimeBounds.MaxTime <= 0)
                throw new InvalidOperationException("Max time must be set.");

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (transaction.TimeBounds.MaxTime - currentTime > 1000)
                throw new InvalidOperationException("Transaction expiration time is to far.");

            if (transaction.Operations.Any(o => !(o is PaymentOperation)))
                throw new InvalidOperationException("Only payment operations are allowed.");

            if (transaction.Operations.Length > 100)
                throw new InvalidOperationException("Too many operations.");

            var stream = new XdrDataOutputStream();
            stellar_dotnet_sdk.xdr.Transaction.Encode(stream, transaction.ToXdrV1());
            return stream.ToArray();
        }

        public override SignatureModel SignTransaction(byte[] transaction)
        {
            if (!StellarTransactionExtensions.TryDeserializeTransaction(transaction, out var tx))
                throw new Exception("Unable to deserialize transaction.");

            if (tx.Signatures.Count > 0)
                throw new ArgumentException("Transaction contains signatures.");

            tx.Sign(secret, dataSource.Network);
            var signature = tx.Signatures.First();
            return new SignatureModel { Signer = secret.PublicKey, Signature = signature.Signature.InnerValue };
        }

        public override void SubmitTransaction(byte[] transaction, List<SignatureModel> signatures)
        {
            if (!StellarTransactionExtensions.TryDeserializeTransaction(transaction, out var tx))
                throw new Exception("Unable to deserialize transaction.");

            if (tx.Signatures.Count > 0)
                throw new ArgumentException("Transaction contains signatures.");

            var accountModel = GetVaultAccount().Result;
            var currentWeight = 0;
            foreach (var signature in signatures)
            {
                var signerKey = KeyPair.FromPublicKey(signature.Signer);
                tx.Signatures.Add(new DecoratedSignature { Hint = signerKey.SignatureHint, Signature = new Signature(signature.Signature) });
                var currentSigner = accountModel.Signers.FirstOrDefault();
                if (currentSigner == null)
                    throw new Exception($"Unknown signer {signerKey.AccountId}");
                currentWeight += currentSigner.Weight;
                if (currentWeight >= accountModel.Thresholds.High)
                    break;
            }
            dataSource.SubmitTransaction(tx).Wait();
        }

        List<DepositModel> GetVaultPayments(stellar_dotnet_sdk.Transaction transaction, bool isSuccess)
        {
            var ledgerPayments = new List<DepositModel>();
            var txHash = transaction.Hash();

            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                var destination = (transaction.Operations[i].SourceAccount?.SigningKey ?? transaction.SourceAccount.SigningKey).PublicKey;
                if (Settings.TryGetDeposit(transaction.Operations[i].ToOperationBody(), destination, isSuccess, txHash, out DepositModel payment))
                    ledgerPayments.Add(payment);
            }
            return ledgerPayments;
        }

        void ProcessTransaction(TxModel tx)
        {
            try
            {
                var payments = GetVaultPayments(stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(tx.EnvelopeXdr), tx.IsSuccess);
                var payment = new DepositNotificationModel
                {
                    ProviderId = Id,
                    Cursor = tx.PagingToken.ToString(),
                    Items = payments,
                    DepositTime = DateTime.UtcNow
                };

                NotificationsManager.RegisterNotification(payment);
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();

                //if worker is broken, the auditor should quit consensus
                RaiseOnError(e);
                return;
            }
        }

        async Task ListenTransactions()
        {
            var failedDates = new List<DateTime>();
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (failedDates.Count > 0)
                    await Task.Delay(new TimeSpan(0, 1, 0));

                var listener = default(TxListener);
                try
                {
                    listener = dataSource.GetTransactionListener(
                        Vault,
                        long.Parse(LastRegisteredCursor),
                        ProcessTransaction
                    );

                    await listener.Connect();
                }
                catch (Exception exc)
                {
                    //dispose current listener instance
                    listener?.Shutdown();
                    listener?.Dispose();

                    //clear if last fail was long ago
                    if (failedDates.Count > 0 && DateTime.UtcNow - failedDates.LastOrDefault() > new TimeSpan(0, 10, 0))
                        failedDates.Clear();
                    failedDates.Add(DateTime.UtcNow);
                    if (failedDates.Count > 5)
                    {
                        var e = exc;
                        if (exc is AggregateException)
                            e = exc.GetBaseException();

                        RaiseOnError(exc);
                        return;
                    }
                }
            }
        }
        public override int CompareCursors(string left, string right)
        {
            if (!(long.TryParse(left, out var leftLong) && long.TryParse(right, out var rightLong)))
                throw new Exception("Unable to convert cursor to long.");
            return Comparer<long>.Default.Compare(leftLong, rightLong);
        }

        private readonly DataSource dataSource;

        private readonly KeyPair secret;

        public override bool AreSignaturesValid(byte[] transaction, params SignatureModel[] signatures)
        {
            if (signatures.Length < 1)
                throw new ArgumentException("At least one signature must be specified.");

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                var decoratedSignatures = new List<DecoratedSignature>();
                foreach (var signature in signatures)
                {
                    var pubKey = KeyPair.FromPublicKey(signature.Signer);
                    if (!pubKey.Verify(transaction, signature.Signature))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    class Config
    {
        public string Horizon { get; set; }

        public string Secret { get; set; }

        public string PassPhrase { get; set; }
    }
}