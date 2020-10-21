﻿using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class TxManager : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public TxManager(long txCursor)
        {
            TxCursor = txCursor;

            //only auditors listen to the ledger
            if (!Global.IsAlpha && !EnvironmentHelper.IsTest)
            {
                EnsureTxListenerIsDisposed();
                _ = ListenTransactions(TxCursor);
            }
        }

        private object syncRoot = new { };

        public void SetCursor(long cursor)
        {
            lock (syncRoot)
                TxCursor = cursor;
        }

        public bool IsValidNewCursor(long newCursor)
        {
            lock (syncRoot)
                return TxCursor <= newCursor;
        }

        public long TxCursor { get; private set; }

        private void EnsureTxListenerIsDisposed()
        {
            listener?.Shutdown();
            listener?.Dispose();
        }

        private IEventSource listener;

        private async Task ListenTransactions(long cursor)
        {
            listener = Global.StellarNetwork.Server.GetTransactionsRequestBuilder(Global.Constellation.Vault.ToString(), cursor)
                .Stream((_, tx) => awaitedTransactions.Add(tx));

            _ = Task.Factory.StartNew(() =>
            {
                foreach (var tx in awaitedTransactions.GetConsumingEnumerable())
                    ProcessTransactionTx(tx);
            });

            await listener.Connect();
        }

        private BlockingCollection<TransactionResponse> awaitedTransactions = new BlockingCollection<TransactionResponse>();

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
                    if (!(payment is Models.Withdrawal && ledgerPayments.Any(p => ByteArrayPrimitives.Equals(p.TransactionHash, txHash))))
                        ledgerPayments.Add(payment);
                }
            }
            return ledgerPayments;
        }

        private void ProcessTransactionTx(TransactionResponse tx)
        {
            try
            {
                if (Global.AppState.State == ApplicationState.Failed)
                {
                    listener.Shutdown();
                    return;
                }

                var payments = AddVaultPayments(Transaction.FromEnvelopeXdr(tx.EnvelopeXdr), tx.Result.IsSuccess);
                if (payments.Count > 0)
                {
                    var payment = new TxNotification
                    {
                        TxCursor = long.Parse(tx.PagingToken),
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
                logger.Error(e);

                //if worker is broken, the auditor should quit consensus
                Global.AppState.State = ApplicationState.Failed;

                listener?.Shutdown();

                throw;
            }
        }

        public void Dispose()
        {
            listener?.Shutdown();
            listener?.Dispose();
            listener = null;
            awaitedTransactions?.Dispose();
            awaitedTransactions = null;
        }
    }
}