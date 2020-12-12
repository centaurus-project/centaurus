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
    public class QuantumSyncWorker : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumSyncWorker(long apexCursor, AlphaWebSocketConnection auditor)
        {
            this.auditor = auditor;
            this.apexCursor = apexCursor;
            cancellationTokenSource = new CancellationTokenSource();
            SendQuantums();
        }

        private AlphaWebSocketConnection auditor;

        private long apexCursor;

        private CancellationTokenSource cancellationTokenSource;

        private const int batchSize = 100;

        private void SendQuantums()
        {
            Task.Factory.StartNew(async () =>
            {
                while (cancellationTokenSource != null && 
                    !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (auditor.ConnectionState != ConnectionState.Ready
                        && Global.QuantumStorage.CurrentApex - apexCursor <= 100 //the auditor is less than 100 quanta behind
                        ) //the auditor is not in init state
                        auditor.ConnectionState = ConnectionState.Ready;

                    if (apexCursor == Global.QuantumStorage.CurrentApex)
                    {
                        Thread.Sleep(50);
                        continue;
                    }
                    try
                    {
                        List<MessageEnvelope> quanta = null;
                        if (!Global.QuantumStorage.GetQuantaBacth(apexCursor + 1, batchSize, out quanta))
                            quanta = await PersistenceManager.GetQuantaAboveApex(apexCursor, batchSize); //quanta are not found in the in-memory storage

                        quanta = quanta.OrderBy(q => ((Quantum)q.Message).Apex).ToList();
                        logger.Info(() => $"Batch request from {apexCursor + 1}. Batch content: [{string.Join(',', quanta.Select(q => ((Quantum)q.Message).Apex.ToString()))}]");

                        var lastQuantum = quanta.LastOrDefault();
                        if (lastQuantum == null) //it can be null if there is only one quantum in the in-memory quantum storage and it's in progress
                            continue;

                        var batchMessage = new QuantaBatch { Quanta = quanta };
                        await auditor.SendMessage(batchMessage, cancellationTokenSource.Token);
 
                        apexCursor = ((Quantum)lastQuantum.Message).Apex;
                    }
                    catch (Exception exc)
                    {
                        if (exc is ObjectDisposedException 
                        || exc.GetBaseException() is ObjectDisposedException)
                            throw;
                        logger.Error(exc);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }

        public void CancelAndDispose()
        {
            cancellationTokenSource?.Cancel();
            Dispose();
        }
    }
}
