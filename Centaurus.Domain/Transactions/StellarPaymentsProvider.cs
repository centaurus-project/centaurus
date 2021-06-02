using Centaurus.Models;
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

namespace Centaurus.Domain
{
    public class StellarPaymentsProvider : PaymentsProviderBase<KeyPair, KeyPair, long>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public StellarPaymentsProvider(ExecutionContext context)
            : base(context)
        {
        }

        public override PaymentProvider Provider => PaymentProvider.Stellar;

        TimeSpan commitDelay = TimeSpan.FromMinutes(5);

        public override void Init(string vault, string cursor, string secret, PaymentsParserBase paymentsParser, WithdrawalStorage withdrawals)
        {
            base.Init(vault, cursor, secret, paymentsParser, withdrawals);
            Task.Factory.StartNew(ListenTransactions, TaskCreationOptions.LongRunning);
            InitTimer();
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

        object timerSyncRoot = new { };

        List<PaymentBase> GetVaultPayments(stellar_dotnet_sdk.Transaction transaction, bool isSuccess)
        {
            var ledgerPayments = new List<PaymentBase>();
            var res = isSuccess ? PaymentResults.Success : PaymentResults.Failed;
            var txHash = transaction.Hash();
            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                var source = transaction.Operations[i].SourceAccount?.SigningKey ?? transaction.SourceAccount.SigningKey;
                if (Context.TryGetPayment(transaction.Operations[i].ToOperationBody(), source, Vault, res, txHash, out PaymentBase payment))
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
                    Provider = PaymentProvider.Stellar,
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
                Context.AppState.State = ApplicationState.Failed;

                throw;
            }
        }

        async Task ListenTransactions()
        {
            var failedDates = new List<DateTime>();
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var listener = default(StellarTxListenerBase);
                try
                {
                    listener = Context.StellarDataProvider.GetTransactionListener(
                        Vault.AccountId,
                        LastRegisteredCursor,
                        ProcessTransactionTx
                    );

                    await listener.Connect();
                }
                catch (Exception exc)
                {
                    if (Context.AppState.State == ApplicationState.Failed)
                        return;

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

                        Context.AppState.State = ApplicationState.Failed;
                        throw;
                    }
                    //TODO: discuss if we should wait
                    await Task.Delay(new TimeSpan(0, 1, 0));
                }
            }
        }


        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        System.Timers.Timer submitTimer = new System.Timers.Timer();

        void InitTimer()
        {
            lock (timerSyncRoot)
            {
                submitTimer.Interval = 60 * 1000;
                submitTimer.AutoReset = false;
                submitTimer.Elapsed += SubmitTimer_Elapsed;
                submitTimer.Start();
            }
        }

        private async void SubmitTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Context.IsAlpha)
                foreach (var payment in NotificationsManager.GetAll())
                {
                    if (DateTime.UtcNow - payment.PaymentTime < commitDelay)
                        break;
                    _ = Context.QuantumHandler.HandleAsync(new PaymentCommitQuantum { Source = payment.Payment }.CreateEnvelope());
                }

            await CleanupWithdrawals();

            lock (timerSyncRoot)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                    submitTimer.Start();
            }
        }

        #region Withdrawals

        async Task CleanupWithdrawals()
        {
            var currentTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiredTransactions = WithdrawalStorage.GetAll().Where(w => w.IsExpired(currentTimeSeconds)).Select(w => w.Hash).ToArray();

            if (expiredTransactions.Length < 1)
                return;

            //we must ignore all transactions that was submitted. TxListener will handle submitted transactions.
            var unhandledTxs = await GetUnhandledTx();
            foreach (var expiredTransaction in expiredTransactions.Where(tx => !unhandledTxs.Contains(tx, ByteArrayComparer.Default)))
                _ = Context.QuantumHandler.HandleAsync(new WithrawalsCleanupQuantum { ExpiredWithdrawal = expiredTransaction }.CreateEnvelope());
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
                    var result = await Context.StellarDataProvider.GetTransactions(Vault.AccountId, Cursor, limit);
                    while (result.Count > 0)
                    {
                        unhandledTxs.AddRange(result.Select(r => ByteArrayExtensions.FromHexString(r.Hash)));
                        if (result.Count != limit)
                            break;
                        result = await Context.StellarDataProvider.GetTransactions(Vault.AccountId, result.Last().PagingToken, limit);
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

        public override Ed25519Signature SignTransaction(TransactionWrapper transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));
            return transaction.Hash.Sign(Secret);
        }

        #endregion
    }
}