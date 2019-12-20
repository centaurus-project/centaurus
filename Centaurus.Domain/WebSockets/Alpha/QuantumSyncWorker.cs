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

        private void SendQuantums()
        {
            Task.Factory.StartNew(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (apexCursor == Global.QuantumStorage.CurrentApex)
                    {
                        Thread.Sleep(50);
                        continue;
                    }
                    try
                    {
                        var quantumEnvelope = Global.QuantumStorage.GetQuantum(apexCursor + 1);
                        var quantum = (Quantum)quantumEnvelope.Message;
                        if (!quantum.IsProcessed)
                            continue;
                        await auditor.SendMessage(quantumEnvelope, cancellationTokenSource.Token);
                        apexCursor = ((Quantum)quantumEnvelope.Message).Apex;
                    }
                    catch (Exception exc)
                    {
                        logger.Error(exc);
                    }
                }
            });
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
