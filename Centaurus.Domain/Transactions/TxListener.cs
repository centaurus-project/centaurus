using Centaurus.Models;
using Centaurus.Stellar;
using Centaurus.Stellar.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class TxListenerBase : IContextual
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public TxListenerBase(ExecutionContext context, long txCursor)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Task.Factory.StartNew(() => ListenTransactions(txCursor), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() =>
            {
                try
                {
                    foreach (var tx in awaitedTransactions.GetConsumingEnumerable(cancellationTokenSource.Token))
                        ProcessTransactionTx(tx);
                }
                catch (OperationCanceledException) { }
                catch (Exception exc)
                {
                    logger.Error(exc);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public ExecutionContext Context { get; }

        protected abstract void ProcessTransactionTx(TxModel tx);

        protected async Task ListenTransactions(long cursor)
        {
            var failedDates = new List<DateTime>();
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    listener = Context.StellarDataProvider.GetTransactionListener(Context.Constellation.Vault.ToString(), cursor, (tx) => awaitedTransactions.Add(tx));
                    await listener.Connect();
                }
                catch (Exception exc)
                {
                    DisposeListener();
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
                    //continue from last known cursor
                    cursor = Context.TxCursorManager.TxCursor;
                }
            }
        }

        protected StellarTxListenerBase listener;

        protected BlockingCollection<TxModel> awaitedTransactions = new BlockingCollection<TxModel>();

        protected CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
            DisposeListener();
            awaitedTransactions?.Dispose();
            awaitedTransactions = null;
        }

        private void DisposeListener()
        {
            listener?.Shutdown();
            listener?.Dispose();
            listener = null;
        }
    }

    public class AuditorTxListener : TxListenerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AuditorTxListener(ExecutionContext context, long cursor)
            : base(context, cursor)
        {
        }

        private AuditorContext AuditorContext => (AuditorContext)Context;

        private List<PaymentBase> AddVaultPayments(Transaction transaction, bool isSuccess)
        {
            var ledgerPayments = new List<PaymentBase>();
            var res = isSuccess ? PaymentResults.Success : PaymentResults.Failed;
            var txHash = transaction.Hash();
            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                var source = transaction.Operations[i].SourceAccount?.SigningKey ?? transaction.SourceAccount.SigningKey;
                if (Context.TryGetPayment(transaction.Operations[i].ToOperationBody(), source, res, txHash, out PaymentBase payment))
                {
                    //withdrawals are grouped by tx hash. If one withdrawal item already in list, then we can skip this one
                    if (ledgerPayments.Any(p => p is Withdrawal))
                        continue;
                    ledgerPayments.Add(payment);
                }
            }
            return ledgerPayments;
        }

        protected override void ProcessTransactionTx(TxModel tx)
        {
            try
            {
                if (Context.AppState.State == ApplicationState.Failed)
                {
                    listener.Shutdown();
                    return;
                }

                var payments = AddVaultPayments(Transaction.FromEnvelopeXdr(tx.EnvelopeXdr), tx.IsSuccess);
                var payment = new TxNotification
                {
                    TxCursor = tx.PagingToken,
                    Payments = payments
                };

                logger.Trace($"Tx with hash {tx.Hash} is handled. Number of payments for account {Context.Constellation.Vault} is {payment.Payments.Count}.");

                AuditorContext.OutgoingMessageStorage.OnTransaction(payment);
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();
                logger.Error(e, "Transaction listener failed.");

                //if worker is broken, the auditor should quit consensus
                Context.AppState.State = ApplicationState.Failed;

                listener?.Shutdown();


                throw;
            }
        }
    }

    public class AlphaTxListener : TxListenerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaTxListener(ExecutionContext context, long cursor)
            : base(context, cursor)
        {
        }

        private ConcurrentQueue<long> pendingTxCursors = new ConcurrentQueue<long>();

        private void RegisterNewCursor(long cursor)
        {
            pendingTxCursors.Enqueue(cursor);
        }

        public long PeekCursor()
        {
            pendingTxCursors.TryPeek(out var cursor);
            return cursor;
        }

        public void DequeueCursor()
        {
            if (!pendingTxCursors.TryDequeue(out var cursor))
                throw new Exception("Unable to dequeue cursor.");
        }

        protected override void ProcessTransactionTx(TxModel tx)
        {
            try
            {
                if (Context.AppState.State == ApplicationState.Failed)
                {
                    listener.Shutdown();
                    return;
                }

                RegisterNewCursor(tx.PagingToken);
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();
                logger.Error(e, "Transaction listener failed.");

                //if worker is broken, the auditor should quit consensus
                Context.AppState.State = ApplicationState.Failed;

                listener?.Shutdown();

                throw;
            }
        }
    }
}
