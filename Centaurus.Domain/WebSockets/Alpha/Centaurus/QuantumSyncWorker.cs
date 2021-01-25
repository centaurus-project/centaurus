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
            cancellationToken = cancellationTokenSource.Token;
            SendQuantums();
        }

        private AlphaWebSocketConnection auditor;

        private long apexCursor;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        private const int batchSize = 25;

        private void SendQuantums()
        {
            var quantumStorage = (AlphaQuantumStorage)Global.QuantumStorage;
            Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (auditor.ConnectionState != ConnectionState.Ready
                        && quantumStorage.CurrentApex - apexCursor <= 100 //the auditor is less than 100 quanta behind
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
                        if (!quantumStorage.GetQuantaBacth(apexCursor + 1, batchSize, out quanta))
                            quanta = await Global.PersistenceManager.GetQuantaAboveApex(apexCursor, batchSize); //quanta are not found in the in-memory storage

                        quanta = quanta.OrderBy(q => ((Quantum)q.Message).Apex).ToList();

                        var batchMessage = new QuantaBatch { Quanta = quanta };
                        await auditor.SendMessage(batchMessage);
 
                        apexCursor = ((Quantum)quanta.Last().Message).Apex;
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
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
}
