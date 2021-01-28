using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class TxListenerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public TxListenerBase(long txCursor)
        {
            _ = ListenTransactions(txCursor);
            _ = Task.Factory.StartNew(() =>
            {
                foreach (var tx in awaitedTransactions.GetConsumingEnumerable())
                    ProcessTransactionTx(tx);
            }, TaskCreationOptions.LongRunning);
        }

        protected abstract void ProcessTransactionTx(TransactionResponse tx);

        protected async Task ListenTransactions(long cursor)
        {
            var failedDates = new List<DateTime>();
            while (true)
            {
                try
                {
                    listener = Global.StellarNetwork.Server.GetTransactionsRequestBuilder(Global.Constellation.Vault.ToString(), cursor)
                        .Stream((_, tx) =>
                        {
                            awaitedTransactions.Add(tx);
                        });

                    await listener.Connect();
                }
                catch (Exception exc)
                {
                    listener.Shutdown();
                    listener.Dispose();
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

                        Global.AppState.State = ApplicationState.Failed;
                        throw;
                    }
                    //TODO: discuss if we should wait
                    await Task.Delay(new TimeSpan(0, 1, 0));
                    //continue from last known cursor
                    cursor = Global.TxCursorManager.TxCursor;
                }
            }
        }

        protected IEventSource listener;

        protected BlockingCollection<TransactionResponse> awaitedTransactions = new BlockingCollection<TransactionResponse>();

        public void Dispose()
        {
            listener?.Shutdown();
            listener?.Dispose();
            listener = null;
            awaitedTransactions?.Dispose();
            awaitedTransactions = null;
        }
    }

    public class AuditorTxListener : TxListenerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AuditorTxListener(long cursor)
            : base(cursor)
        {
        }

        private List<PaymentBase> AddVaultPayments(Transaction transaction, bool isSuccess)
        {
            var ledgerPayments = new List<PaymentBase>();
            var res = isSuccess ? PaymentResults.Success : PaymentResults.Failed;
            var txHash = transaction.Hash();
            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                var source = transaction.Operations[i].SourceAccount?.SigningKey ?? transaction.SourceAccount.SigningKey;
                if (PaymentsHelper.FromOperationResponse(transaction.Operations[i].ToOperationBody(), source, res, txHash, out PaymentBase payment))
                {
                    //withdrawals are grouped by tx hash. If one withdrawal item already in list, then we can skip this one
                    if (ledgerPayments.Any(p => p is Withdrawal))
                        continue;
                    ledgerPayments.Add(payment);
                }
            }
            return ledgerPayments;
        }

        protected override void ProcessTransactionTx(TransactionResponse tx)
        {
            try
            {
                if (Global.AppState.State == ApplicationState.Failed)
                {
                    listener.Shutdown();
                    return;
                }
                var pagingToken = long.Parse(tx.PagingToken);

                var payments = AddVaultPayments(Transaction.FromEnvelopeXdr(tx.EnvelopeXdr), tx.Result.IsSuccess);
                if (payments.Count > 0)
                {
                    var payment = new TxNotification
                    {
                        TxCursor = pagingToken,
                        Payments = payments
                    };

                    logger.Trace($"Tx with hash {tx.Hash} is handled. Number of payments for account {Global.Constellation.Vault} is {payment.Payments.Count}.");

                    OutgoingMessageStorage.OnTransaction(payment);
                }
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();
                logger.Error(e, "Transaction listener failed.");

                //if worker is broken, the auditor should quit consensus
                Global.AppState.State = ApplicationState.Failed;

                listener?.Shutdown();


                throw;
            }
        }
    }

    public class AlphaTxListener : TxListenerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaTxListener(long cursor)
            : base(cursor)
        {
        }

        private Queue<long> pendingTxCursors = new Queue<long>();

        private void RegisterNewCursor(long cursor)
        {
            lock (pendingTxCursors)
                pendingTxCursors.Enqueue(cursor);
            OnNewCursor.Invoke();
        }

        public long PeekCursor()
        { 
            lock (pendingTxCursors)
            {
                pendingTxCursors.TryPeek(out var cursor);
                return cursor;
            }
        }

        public void DequeueCursor()
        {
            lock (pendingTxCursors)
            {
                if (!pendingTxCursors.TryDequeue(out var cursor))
                    throw new Exception("Unable to dequeue cursor.");
            }
        }

        public event Action OnNewCursor;

        protected override void ProcessTransactionTx(TransactionResponse tx)
        {
            try
            {
                if (Global.AppState.State == ApplicationState.Failed)
                {
                    listener.Shutdown();
                    return;
                }
                var transaction = Transaction.FromEnvelopeXdr(tx.EnvelopeXdr);
                if (!transaction.Operations.Any(o => PaymentsHelper.SupportedDepositOperations.Contains(o.ToOperationBody().Discriminant.InnerValue)))
                    return;
                RegisterNewCursor(long.Parse(tx.PagingToken));
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();
                logger.Error(e, "Transaction listener failed.");

                //if worker is broken, the auditor should quit consensus
                Global.AppState.State = ApplicationState.Failed;

                listener?.Shutdown();

                throw;
            }
        }
    }
}
