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

        private const int batchSize = 100_000;

        private void SendQuantums()
        {
            Task.Factory.StartNew(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (auditor.ConnectionState != ConnectionState.Ready
                        && Global.QuantumStorage.CurrentApex - apexCursor <= 100 //the auditor is less than 100 quanta behind
                        && apexCursor != 0) //the auditor is not in init state
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
                            quanta = await SnapshotManager.GetQuantaAboveApex(apexCursor, batchSize); //quanta are not found in the in-memory storage

                        var lastQuantum = quanta.LastOrDefault();
                        if (lastQuantum == null) //it can be null if there is only one quantum in the in-memory quantum storage and it's in progress
                            continue;

                        var batchMessage = new QuantaBatch { Quanta = quanta };
                        await auditor.SendMessage(batchMessage, cancellationTokenSource.Token);
 
                        if (apexCursor == 0) //if apex cursor is set to 0 than the auditor is in init state, and it will send new set apex cursor message after init 
                            break;
                        apexCursor = ((Quantum)lastQuantum.Message).Apex;
                    }
                    catch (Exception exc)
                    {
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
