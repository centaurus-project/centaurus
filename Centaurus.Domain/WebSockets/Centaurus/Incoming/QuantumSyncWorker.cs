using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public class QuantumSyncWorker : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumSyncWorker(Domain.ExecutionContext context, ulong apexCursor, ConnectionBase auditor)
            :base(context)
        {
            this.auditor = auditor ?? throw new ArgumentNullException(nameof(auditor));
            CurrentApexCursor = apexCursor;
            Task.Factory.StartNew(SendQuantums, TaskCreationOptions.LongRunning);
        }

        private readonly ConnectionBase auditor;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public ulong CurrentApexCursor { get; private set; }

        private async Task SendQuantums()
        {
            var batchSize = Context.Settings.SyncBatchSize;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var apexDiff = Context.QuantumStorage.CurrentApex - CurrentApexCursor;
                if (apexDiff < 0)
                {
                    logger.Error($"Auditor {((KeyPair)auditor.PubKey).AccountId} is above current constellation state.");
                    await auditor.CloseConnection(System.Net.WebSockets.WebSocketCloseStatus.ProtocolError, "Auditor is above all constellation.");
                    return;
                }

                if (!Context.IsAlpha || auditor.ConnectionState != ConnectionState.Ready || apexDiff == 0 || Context.StateManager.State == State.Rising)
                {
                    Thread.Sleep(50);
                    continue;
                }
                try
                {
                    List<PendingQuantum> quanta = null;
                    if (!Context.QuantumStorage.GetQuantaBacth(CurrentApexCursor + 1, batchSize, out quanta))
                    {
                        quanta = Context.PersistenceManager.GetQuantaAboveApex(CurrentApexCursor, batchSize); //quanta are not found in the in-memory storage
                        if (quanta.Count < 1)
                            throw new Exception("No quanta from database.");
                    }

                    if (quanta.Count < 1)
                        throw new Exception("No quanta from storage.");

                    var firstApex = quanta.First().Quantum.Apex;
                    var lastApex = quanta.Last().Quantum.Apex;

                    logger.Trace($"About to sent {quanta.Count} quanta. Apex from {firstApex} to {lastApex}");

                    var batchMessage = new QuantaBatch { Quanta = quanta };
                    await auditor.SendMessage(batchMessage);

                    CurrentApexCursor = lastApex;
                }
                catch (Exception exc)
                {
                    if (exc is ObjectDisposedException
                    || exc.GetBaseException() is ObjectDisposedException)
                        throw;
                    logger.Error(exc, $"Unable to get quanta. Cursor: {CurrentApexCursor}; CurrentApex: {Context.QuantumStorage.CurrentApex}");
                }
            }
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}