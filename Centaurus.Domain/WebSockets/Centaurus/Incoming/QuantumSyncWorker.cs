using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public class QuantumSyncWorker : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumSyncWorker(Domain.ExecutionContext context, ConnectionBase auditor)
            : base(context)
        {
            this.auditor = auditor ?? throw new ArgumentNullException(nameof(auditor));
            batchSize = Context.Settings.SyncBatchSize;
            Task.Factory.StartNew(SendQuantums, TaskCreationOptions.LongRunning);
        }

        private readonly ConnectionBase auditor;
        private readonly int batchSize;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);

        public ulong CurrentQuantaCursor { get; private set; }
        public ulong? CurrentResultCursor { get; private set; }

        public void SetCursors(ulong quantaCursor, ulong? resultCursor)
        {
            syncRoot.Wait();
            try
            {
                CurrentQuantaCursor = quantaCursor;
                CurrentResultCursor = resultCursor;
            }
            finally
            {
                syncRoot.Release();
            }
        }

        private async Task SendQuantums()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await syncRoot.WaitAsync();
                try
                {
                    if (CurrentQuantaCursor > Context.QuantumStorage.CurrentApex)
                    {
                        logger.Error($"Auditor {auditor.PubKey.GetAccountId()} is above current constellation state.");
                        await auditor.CloseConnection(System.Net.WebSockets.WebSocketCloseStatus.ProtocolError, "Auditor is above all constellation.");
                        return;
                    }

                    var quantaDiff = Context.QuantumStorage.CurrentApex - CurrentQuantaCursor;
                    var resultDiff = (Context.PendingUpdatesManager.LastSavedApex - CurrentResultCursor) ?? 0;

                    if (!Context.IsAlpha //only Alpha should broadcast quanta
                        || auditor.ConnectionState != ConnectionState.Ready //connection is not validated yet
                        || (quantaDiff == 0 && resultDiff == 0) //nothing to sync
                        || Context.StateManager.State == State.Rising) //wait for Running state
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    var quantaBatch = quantaDiff > 0
                        ? GetPendingQuanta(CurrentQuantaCursor, batchSize)
                        : new List<PendingQuantum>();

                    var batchMessage = new QuantaBatch
                    {
                        Quanta = quantaBatch.Select(q => (Message)q.Quantum).ToList(),
                        LastKnownApex = Context.QuantumStorage.CurrentApex
                    };

                    var firstQuantumApex = quantaBatch.FirstOrDefault()?.Quantum.Apex;
                    var lastQuantumApex = quantaBatch.LastOrDefault()?.Quantum.Apex;

                    if (resultDiff != 0)
                    {
                        var resultCursor = CurrentResultCursor.Value;
                        var count = Math.Min(batchSize, (int)(Context.PendingUpdatesManager.LastSavedApex - resultCursor));

                        var signaturesQuanta = default(IEnumerable<PendingQuantum>);
                        if (quantaBatch.Count > 0 && firstQuantumApex <= resultCursor && resultCursor + (ulong)count <= lastQuantumApex)
                        {
                            signaturesQuanta = quantaBatch
                                .SkipWhile(q => q.Quantum.Apex != resultCursor + 1)
                                .Take(count);
                        }
                        else
                            //TODO:intersect with loaded already quanta
                            signaturesQuanta = GetPendingQuanta(resultCursor, count);

                        batchMessage.Signatures = signaturesQuanta
                            .Select(q => new QuantumSignatures
                            {
                                Apex = q.Quantum.Apex,
                                Signatures = q.Signatures
                            })
                            .ToList();
                    }

                    var lastResultApex = batchMessage.Signatures?.LastOrDefault()?.Apex;

                    logger.Trace($"About to sent {batchMessage.Quanta.Count} quanta and {batchMessage.Signatures?.Count} results. Quanta from {firstQuantumApex} to {lastQuantumApex}.");

                    await auditor.SendMessage(batchMessage.CreateEnvelope<MessageEnvelopeSignless>());

                    if (lastQuantumApex.HasValue)
                        CurrentQuantaCursor = lastQuantumApex.Value;

                    if (lastResultApex.HasValue)
                        CurrentResultCursor = lastResultApex;
                }
                catch (Exception exc)
                {
                    if (exc is ObjectDisposedException
                    || exc.GetBaseException() is ObjectDisposedException)
                        throw;
                    logger.Error(exc, $"Unable to get quanta. Cursor: {CurrentQuantaCursor}; CurrentApex: {Context.QuantumStorage.CurrentApex}");
                }
                finally
                {
                    syncRoot.Release();
                }
            }
        }

        private List<PendingQuantum> GetPendingQuanta(ulong from, int count)
        {
            if (!Context.QuantumStorage.GetQuantaBacth(from, count, out var quanta))
            {
                //quanta are not found in the in-memory storage
                quanta = Context.DataProvider.GetQuantaAboveApex(from, count);
                if (quanta.Count < 1)
                    throw new Exception("No quanta from database.");
            }
            return quanta;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}