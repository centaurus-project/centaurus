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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Stellar.PaymentProvider
{
    public class StellarPaymentProvider : PaymentProviderBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public StellarPaymentProvider(PaymentParserBase parser, ProviderSettings settings, dynamic config, WithdrawalStorage withdrawalStorage)
            : base(parser, settings, (object)config, withdrawalStorage)
        {
            commitDelay = TimeSpan.FromTicks(settings.PaymentSubmitDelay);
            submitTimerInterval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            maxTxSubmitDelay = config.MaxTxSubmitDelay;
            dataSource = new DataSource(settings.Name, config.Horizon);

            Task.Factory.StartNew(ListenTransactions, TaskCreationOptions.LongRunning);
            InitTimer();
        }

        public override void ValidateTransaction(TransactionWrapper transactionWrapper)
        {
            var transaction = (stellar_dotnet_sdk.Transaction)transactionWrapper.Transaction;
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
        }

        object timerSyncRoot = new { };

        List<PaymentBase> GetVaultPayments(stellar_dotnet_sdk.Transaction transaction, bool isSuccess)
        {
            var ledgerPayments = new List<PaymentBase>();
            var res = isSuccess ? PaymentResults.Success : PaymentResults.Failed;
            var txHash = transaction.Hash();
            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                var source = transaction.Operations[i].SourceAccount?.SigningKey ?? transaction.SourceAccount.SigningKey;
                if (Settings.TryGetPayment(transaction.Operations[i].ToOperationBody(), source, res, txHash, out PaymentBase payment))
                {
                    //withdrawals are grouped by tx hash. If one withdrawal item already in list, then we can skip this one
                    if (ledgerPayments.Any(p => p is Withdrawal))
                        continue;
                    ledgerPayments.Add(payment);
                }
            }
            return ledgerPayments;
        }

        void ProcessTransactionTx(TxModel tx)
        {
            try
            {
                var payments = GetVaultPayments(stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(tx.EnvelopeXdr), tx.IsSuccess);
                var payment = new PaymentNotification
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

        public override void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                lock (timerSyncRoot)
                {
                    submitTimer.Stop();
                    submitTimer.Dispose();
                }
            }
        }


        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        System.Timers.Timer submitTimer = new System.Timers.Timer();

        readonly TimeSpan commitDelay;
        readonly double submitTimerInterval;
        private readonly long maxTxSubmitDelay;
        private readonly DataSource dataSource;

        void InitTimer()
        {
            lock (timerSyncRoot)
            {
                submitTimer.Interval = submitTimerInterval;
                submitTimer.AutoReset = false;
                submitTimer.Elapsed += SubmitTimer_Elapsed;
                StartTimer();
            }
        }

        async void SubmitTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CommitPayments();

            await CleanupWithdrawals();

            StartTimer();
        }

        void StartTimer()
        {

            lock (timerSyncRoot)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                    submitTimer.Start();
            }
        }

        void CommitPayments()
        {
            foreach (var payment in NotificationsManager.GetAll())
            {
                if (DateTime.UtcNow - payment.PaymentTime < commitDelay)
                    break;
                RaiseOnPaymentCommit(new PaymentCommitQuantum { Source = payment.Payment }.CreateEnvelope());
            }
        }

        #region Withdrawals

        async Task CleanupWithdrawals()
        {
            var currentTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiredTransactions = WithdrawalStorage.GetAll().Where(w => w.IsExpired(currentTimeSeconds, maxTxSubmitDelay)).Select(w => w.Hash).ToArray();

            if (expiredTransactions.Length < 1)
                return;

            //we must ignore all transactions that was submitted. TxListener will handle submitted transactions.
            var unhandledTxs = await GetUnhandledTx();
            foreach (var expiredTransaction in expiredTransactions.Where(tx => !unhandledTxs.Contains(tx, ByteArrayComparer.Default)))
                RaiseOnCleanup(new WithrawalsCleanupQuantum { ExpiredWithdrawal = expiredTransaction }.CreateEnvelope());
        }

        async Task<List<byte[]>> GetUnhandledTx()
        {
            var retries = 1;
            while (true)
            {
                try
                {
                    var limit = 200;
                    var unhandledTxs = new List<byte[]>();
                    var result = await dataSource.GetTransactions(Vault, long.Parse(Cursor), limit);
                    while (result.Count > 0)
                    {
                        unhandledTxs.AddRange(result.Select(r => ByteArrayExtensions.FromHexString(r.Hash)));
                        if (result.Count != limit)
                            break;
                        result = await dataSource.GetTransactions(Vault, result.Last().PagingToken, limit);
                    }
                    return unhandledTxs;
                }
                catch
                {
                    if (retries == 5)
                        throw;
                    await Task.Delay(retries * 1000);
                    retries++;
                }
            }
        }

        public override TxSignature SignTransaction(TransactionWrapper transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));
            var signature = transaction.Hash.Sign(KeyPair.FromSecretSeed(Secret));
            return new TxSignature { Signature = signature.Signature, Signer = signature.Signer };
        }

        public override WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, AccountWrapper account, TransactionWrapper transactionWrapper)
        {
            return Parser.GetWithdrawal(envelope, account, transactionWrapper, Settings);
        }

        #endregion
    }
}