using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditLedgerManager : MajorityManager
    {
        public AuditLedgerManager(AlphaContext context)
            : base(context)
        {
            alphaListener = (AlphaTxListener)context.TxListener;
            Task.Factory.StartNew(TryHandleTxCommit, TaskCreationOptions.LongRunning);
        }

        private Dictionary<long, TxCommitQuantum> pendingTxCommits = new Dictionary<long, TxCommitQuantum>();
        private AlphaTxListener alphaListener;
        private object syncRoot = new { };

        protected override void OnResult(MajorityResults majorityResult, MessageEnvelope result)
        {
            base.OnResult(majorityResult, result);
            if (majorityResult != MajorityResults.Success)
            {
                logger.Error($"Tx result received ({majorityResult}).");
                Context.AppState.State = ApplicationState.Failed;
            }

            var nextCursor = alphaListener.PeekCursor();
            var receivedCursor = ((TxNotification)result.Message).TxCursor;
            if (nextCursor > receivedCursor)
            {
                logger.Warn($"Delayed tx result. Next awaited cursor is {nextCursor}, but {receivedCursor} was received.");
                return;
            }
            AddNewTxCommit(result);
        }

        private void AddNewTxCommit(MessageEnvelope result)
        {
            lock (syncRoot)
            {
                var txCursor = ((TxNotification)result.Message).TxCursor;
                if (!pendingTxCommits.ContainsKey(txCursor))
                    pendingTxCommits.Add(txCursor, new TxCommitQuantum { Source = result });
            }
        }

        private bool TryGetTxCommit(long cursor, out TxCommitQuantum txCommit)
        {
            lock (syncRoot)
                return pendingTxCommits.Remove(cursor, out txCommit);
        }

        private void TryHandleTxCommit()
        {
            try
            {
                while (!isStoped)
                {
                    var nextCursor = alphaListener.PeekCursor();
                    if (TryGetTxCommit(nextCursor, out var txCommit))
                    {
                        alphaListener.DequeueCursor();
                        var ledgerCommitEnvelope = txCommit.CreateEnvelope();
                        Context.QuantumHandler.HandleAsync(ledgerCommitEnvelope);
                    }
                    else
                        Thread.Sleep(100);
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on tx commit processing.");
                Context.AppState.State = ApplicationState.Failed;
            }
        }

        private bool isStoped = false;

        public override void Dispose()
        {
            isStoped = true;
            base.Dispose();
        }
    }
}
