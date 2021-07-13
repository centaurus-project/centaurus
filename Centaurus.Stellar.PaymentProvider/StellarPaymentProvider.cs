using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.Stellar;
using Centaurus.Stellar.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Stellar.PaymentProvider
{
    public class StellarPaymentProvider : PaymentProviderBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public StellarPaymentProvider(ProviderSettings settings, string config)
            : base(settings, config)
        {
            commitDelay = TimeSpan.FromSeconds(settings.PaymentSubmitDelay);
            submitTimerInterval = TimeSpan.FromSeconds(5).TotalMilliseconds;

            if (config == null)
                throw new ArgumentNullException(nameof(config));
            var configObject = JsonSerializer.Deserialize<Config>(config);

            dataSource = new DataSource(configObject.PassPhrase, configObject.Horizon);

            secret = stellar_dotnet_sdk.KeyPair.FromSecretSeed(configObject.Secret);

            Task.Factory.StartNew(ListenTransactions, TaskCreationOptions.LongRunning);
            InitTimer();
        }

        public override void ValidateTransaction(byte[] rawTransaction, WithdrawalRequest withdrawalRequest)
        {
            if (rawTransaction == null)
                throw new ArgumentNullException(nameof(rawTransaction));

            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));

            if (!ByteArrayComparer.Default.Equals(rawTransaction.ComputeHash(), BuildTransaction(withdrawalRequest).ComputeHash()))
                throw new BadRequestException($"Transaction is not equal to expected one.");
        }

        private bool ValidateFee(uint fee)
        {
            return true;
        }

        private stellar_dotnet_sdk.Account GetSourceAccount()
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

        public override byte[] BuildTransaction(WithdrawalRequest withdrawalRequest)
        {
            if (withdrawalRequest == null)
                throw new ArgumentNullException(nameof(withdrawalRequest));

            if (!ValidateFee((uint)withdrawalRequest.Fee))
                throw new BadRequestException($"Not fair fee {withdrawalRequest.Fee}.");
            var options = new TransactionBuilderOptions(GetSourceAccount(), (uint)withdrawalRequest.Fee);
            if (!Settings.TryGetAsset(withdrawalRequest.Asset, out var stellarAsset))
                throw new BadRequestException($"Asset {withdrawalRequest.Asset} is not supported by provider.");

            var transaction = TransactionHelper.BuildPaymentTransaction(options, stellar_dotnet_sdk.KeyPair.FromAccountId(withdrawalRequest.Destination), stellarAsset, (long)withdrawalRequest.Amount);
            var txSourceAccount = transaction.SourceAccount;
            if (Vault == txSourceAccount.AccountId)
                throw new BadRequestException("Vault account cannot be used as transaction source.");

            if (transaction.TimeBounds == null || transaction.TimeBounds.MaxTime <= 0)
                throw new BadRequestException("Max time must be set.");

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (transaction.TimeBounds.MaxTime - currentTime > 1000)
                throw new BadRequestException("Transaction expiration time is to far.");

            if (transaction.Operations.Any(o => !(o is PaymentOperation)))
                throw new BadRequestException("Only payment operations are allowed.");

            if (transaction.Operations.Length > 100)
                throw new BadRequestException("Too many operations.");
            
            var stream = new XdrDataOutputStream();
            stellar_dotnet_sdk.xdr.Transaction.Encode(stream, transaction.ToXdrV1());
            return stream.ToArray();
        }

        public override TxSignature SignTransaction(byte[] transaction)
        {
            if (!StellarTransactionExtensions.TryDeserializeTransaction(transaction, out var tx))
                throw new Exception("Unable to deserialize transaction.");

            if (tx.Signatures.Count > 0)
                throw new ArgumentException("Transaction contains signatures.");

            tx.Sign(secret, dataSource.Network);
            var signature = tx.Signatures.First();
            return new TxSignature { Signer = secret.PublicKey, Signature = signature.Signature.InnerValue };
        }

        public override void SubmitTransaction(byte[] transaction, List<TxSignature> signatures)
        {
            if (!StellarTransactionExtensions.TryDeserializeTransaction(transaction, out var tx))
                throw new Exception("Unable to deserialize transaction.");

            if (tx.Signatures.Count > 0)
                throw new ArgumentException("Transaction contains signatures.");

            var accountModel = GetVaultAccount().Result;
            var currentWeight = 0;
            foreach (var signature in signatures)
            {
                var signerKey = stellar_dotnet_sdk.KeyPair.FromPublicKey(signature.Signer);
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

        List<Deposit> GetVaultPayments(stellar_dotnet_sdk.Transaction transaction, bool isSuccess)
        {
            var ledgerPayments = new List<Deposit>();
            var res = isSuccess ? PaymentResults.Success : PaymentResults.Failed;
            var txHash = transaction.Hash();
            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                var source = transaction.Operations[i].SourceAccount?.SigningKey ?? transaction.SourceAccount.SigningKey;
                if (Settings.TryGetDeposit(transaction.Operations[i].ToOperationBody(), source, res, txHash, out Deposit payment))
                    ledgerPayments.Add(payment);
            }
            return ledgerPayments;
        }

        void ProcessTransactionTx(TxModel tx)
        {
            try
            {
                var payments = GetVaultPayments(stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(tx.EnvelopeXdr), tx.IsSuccess);
                var payment = new DepositNotification
                {
                    ProviderId = Id,
                    Cursor = tx.PagingToken.ToString(),
                    Items = payments
                };

                NotificationsManager.RegisterNotification(payment);

                logger.Trace($"Tx with hash {tx.Hash} is handled. Number of payments for account {Vault} is {payment.Items.Count}.");
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();
                logger.Error(e, "Transaction listener failed.");

                //if worker is broken, the auditor should quit consensus
                RaiseOnError(exc);
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
                        ProcessTransactionTx
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
                        logger.Error(e, "Failed to start transaction listener.");

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

        public override void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                lock (submitTimer)
                {
                    submitTimer.Stop();
                    submitTimer.Dispose();
                }
            }
        }


        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly System.Timers.Timer submitTimer = new System.Timers.Timer();

        readonly TimeSpan commitDelay;
        readonly double submitTimerInterval;

        private readonly DataSource dataSource;

        private readonly stellar_dotnet_sdk.KeyPair secret;

        void InitTimer()
        {
            lock (submitTimer)
            {
                submitTimer.Interval = submitTimerInterval;
                submitTimer.AutoReset = false;
                submitTimer.Elapsed += SubmitTimer_Elapsed;
                StartTimer();
            }
        }

        void SubmitTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CommitPayments();

            StartTimer();
        }

        void StartTimer()
        {

            lock (submitTimer)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                    submitTimer.Start();
            }
        }

        void CommitPayments()
        {
            foreach (var payment in NotificationsManager.GetAll())
            {
                if (DateTime.UtcNow - payment.DepositTime < commitDelay)
                    break;
                RaiseOnPaymentCommit(new DepositQuantum { Source = payment.Deposite }.CreateEnvelope());
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