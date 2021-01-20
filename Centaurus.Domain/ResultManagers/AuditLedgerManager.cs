using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditLedgerManager : MajorityManager
    {
        public AuditLedgerManager()
        {
            alphaListener = (AlphaTxListener)Global.TxListener;
            alphaListener.OnNewCursor += AlphaListener_OnNewCursor;
        }

        private Dictionary<long, TxCommitQuantum> pendingTxCommits = new Dictionary<long, TxCommitQuantum>();
        private AlphaTxListener alphaListener;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);

        private void AlphaListener_OnNewCursor()
        {
            _ = TryHandleTxCommit();
        }

        protected override void OnResult(MajorityResults majorityResult, MessageEnvelope result)
        {
            base.OnResult(majorityResult, result);
            if (majorityResult != MajorityResults.Success)
            {
                logger.Error($"Tx result received ({majorityResult}).");
                Global.AppState.State = ApplicationState.Failed;
            }

            var nextCursor = alphaListener.PeekCursor();
            var receivedCursor = ((TxNotification)result.Message).TxCursor;
            if (nextCursor > receivedCursor)
            {
                logger.Warn($"Delayed tx result. Next awaited cursor is {nextCursor}, but {receivedCursor} was received.");
                return;
            }
            AddNewTxCommit(result);
            _ = TryHandleTxCommit();
        }

        private void AddNewTxCommit(MessageEnvelope result)
        {
            semaphore.Wait();
            var txCursor = ((TxNotification)result.Message).TxCursor;
            if (!pendingTxCommits.ContainsKey(((TxNotification)result.Message).TxCursor))
                pendingTxCommits.Add(txCursor, new TxCommitQuantum { Source = result });
            semaphore.Release();
        }

        private async Task TryHandleTxCommit()
        {
            await semaphore.WaitAsync();
            try
            {
                while (true)
                {
                    var nextCursor = alphaListener.PeekCursor();
                    if (pendingTxCommits.Remove(nextCursor, out var txCommit))
                    {
                        alphaListener.DequeueCursor();
                        var ledgerCommitEnvelope = txCommit.CreateEnvelope();
                        await Global.QuantumHandler.HandleAsync(ledgerCommitEnvelope);
                    }
                    else
                        break;
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on tx commit processing.");
                Global.AppState.State = ApplicationState.Failed;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            alphaListener.OnNewCursor -= AlphaListener_OnNewCursor;
            semaphore?.Dispose();
            semaphore = null;
        }
    }
}
