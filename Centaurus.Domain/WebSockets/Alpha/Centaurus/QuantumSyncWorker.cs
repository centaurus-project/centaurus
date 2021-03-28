using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public class QuantumSyncWorker : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumSyncWorker(long apexCursor, AlphaWebSocketConnection auditor)
        {
            this.auditor = auditor;
            CurrentApexCursor = apexCursor;
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            Task.Factory.StartNew(SendQuantums);
        }

        private AlphaWebSocketConnection auditor;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        public long CurrentApexCursor { get; private set; }

        private async Task SendQuantums()
        {
            var batchSize = ((AlphaSettings)Global.Settings).SyncBatchSize;
            var quantumStorage = (AlphaQuantumStorage)Global.QuantumStorage;
            while (!cancellationToken.IsCancellationRequested)
            {
                var apexDiff = quantumStorage.CurrentApex - CurrentApexCursor;
                if (apexDiff < 0)
                {
                    logger.Error($"Auditor {((KeyPair)auditor.ClientPubKey).AccountId} is above current constellation state.");
                    await auditor.CloseConnection(System.Net.WebSockets.WebSocketCloseStatus.ProtocolError, "Auditor is above all constellation.");
                    return;
                }
                if (auditor.ConnectionState != ConnectionState.Ready && apexDiff <= 100)
                {
                    auditor.ConnectionState = ConnectionState.Ready;
                }
                else if (auditor.ConnectionState == ConnectionState.Ready && apexDiff >= 10_000) //auditor is too delayed
                {
                    auditor.ConnectionState = ConnectionState.Validated;
                    return;
                }

                if (CurrentApexCursor == Global.QuantumStorage.CurrentApex)
                {
                    Thread.Sleep(50);
                    continue;
                }
                try
                {
                    List<MessageEnvelope> quanta = null;
                    if (!quantumStorage.GetQuantaBacth(CurrentApexCursor + 1, batchSize, out quanta))
                    {
                        quanta = await Global.PersistenceManager.GetQuantaAboveApex(CurrentApexCursor, batchSize); //quanta are not found in the in-memory storage
                        if (quanta.Count < 1)
                            throw new Exception("No quanta from database.");
                    }

                    if (quanta.Count < 1)
                        throw new Exception("No quanta from storage.");

                    var firstApex = ((Quantum)quanta.First().Message).Apex;
                    var lastApex = ((Quantum)quanta.Last().Message).Apex;

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
                    logger.Error(exc, $"Unable to get quanta. Cursor: {CurrentApexCursor}; CurrentApex: {Global.QuantumStorage.CurrentApex}");
                }
            }
        }

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
}
