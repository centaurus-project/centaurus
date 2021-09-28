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

        public QuantumSyncWorker(Domain.ExecutionContext context, IncomingAuditorConnection auditor)
            : base(context)
        {
            this.auditor = auditor ?? throw new ArgumentNullException(nameof(auditor));
            batchSize = Context.Settings.SyncBatchSize;
            Task.Factory.StartNew(SendQuantums, TaskCreationOptions.LongRunning);
        }

        private readonly IncomingAuditorConnection auditor;
        private readonly int batchSize;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private object syncRoot = new { };

        public ulong? CurrentQuantaCursor { get; private set; }
        public ulong? CurrentResultCursor { get; private set; }

        public void SetCursors(ulong quantaCursor, ulong? resultCursor)
        {
            lock (syncRoot)
            {
                CurrentQuantaCursor = quantaCursor;
                CurrentResultCursor = resultCursor;
            }
        }


        private (ulong? quantaCursor, ulong? resultCursor) GetCursors()
        {
            lock (syncRoot)
                return (CurrentQuantaCursor, CurrentResultCursor);
        }

        private bool IsAuditorReadyToHandleQuanta
        {
            get
            {
                return auditor.ConnectionState == ConnectionState.Ready //connection is validated
                    && (auditor.AuditorState.IsRunning || auditor.AuditorState.IsWaitingForInit); //auditor is ready to handle quanta
            }
        }

        private bool IsCurrentNodeReady => Context.StateManager.State != State.Rising && Context.StateManager.State != State.Undefined;

        private async Task SendQuantums()
        {
            var hasPendingQuanta = true;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!hasPendingQuanta)
                    Thread.Sleep(50);

                var cursors = GetCursors();
                try
                {
                    if (cursors.quantaCursor > Context.QuantumStorage.CurrentApex)
                    {
                        logger.Error($"Auditor {auditor.PubKey.GetAccountId()} is above current constellation state.");
                        await auditor.CloseConnection(System.Net.WebSockets.WebSocketCloseStatus.ProtocolError, "Auditor is above all constellation.");
                        return;
                    }

                    var quantaDiff = (Context.QuantumStorage.CurrentApex - cursors.quantaCursor) ?? 0;
                    var resultDiff = (Context.PendingUpdatesManager.LastSavedApex - cursors.resultCursor) ?? 0;

                    if (!Context.IsAlpha //only Alpha should broadcast quanta
                        || !IsAuditorReadyToHandleQuanta //auditor is not ready to handle quanta
                        || (quantaDiff == 0 && resultDiff == 0) //nothing to sync
                        || !IsCurrentNodeReady)
                    {
                        hasPendingQuanta = false;
                        continue;
                    }

                    var quantaBatch = quantaDiff > 0
                        ? GetPendingQuanta(cursors.quantaCursor.Value, batchSize)
                        : new List<Quantum>();

                    var batchMessage = new QuantaBatch
                    {
                        Quanta = quantaBatch.Select(q => (Message)q).ToList(),
                        LastKnownApex = Context.QuantumStorage.CurrentApex
                    };

                    var firstQuantumApex = quantaBatch.FirstOrDefault()?.Apex;
                    var lastQuantumApex = quantaBatch.LastOrDefault()?.Apex;

                    if (resultDiff != 0)
                    {
                        var resultCursor = cursors.resultCursor.Value;
                        var count = Math.Min(batchSize, (int)(Context.PendingUpdatesManager.LastSavedApex - resultCursor));
                        batchMessage.Signatures = GetAuditorResults(resultCursor, count);
                    }

                    var lastResultApex = batchMessage.Signatures?.LastOrDefault()?.Apex;

                    logger.Trace($"About to sent {batchMessage.Quanta.Count} quanta and {batchMessage.Signatures?.Count ?? 0} results to {auditor.PubKeyAddress}. Quanta from {firstQuantumApex} to {lastQuantumApex}.");

                    await auditor.SendMessage(batchMessage.CreateEnvelope<MessageEnvelopeSignless>());

                    lock (syncRoot)
                    {
                        //if quanta cursor is different, means that auditor requested new cursor
                        if (lastQuantumApex.HasValue && CurrentQuantaCursor == cursors.quantaCursor)
                            CurrentQuantaCursor = lastQuantumApex.Value;

                        if (lastResultApex.HasValue && CurrentResultCursor.HasValue && CurrentResultCursor == cursors.resultCursor)
                            CurrentResultCursor = lastResultApex;
                    }
                }
                catch (Exception exc)
                {
                    if (exc is ObjectDisposedException
                    || exc.GetBaseException() is ObjectDisposedException)
                        throw;
                    logger.Error(exc, $"Unable to get quanta. Cursor: {cursors.quantaCursor}; CurrentApex: {Context.QuantumStorage.CurrentApex}");
                }
            }
        }

        private List<Quantum> GetPendingQuanta(ulong from, int count)
        {
            var quanta = Context.QuantumStorage
                .GetQuanta(from, count)
                .Select(q => q.Quantum)
                .ToList();

            if (quanta.Count < 1)
                throw new Exception($"Quanta from {from} apex not found.");

            return quanta;
        }

        private List<QuantumSignatures> GetAuditorResults(ulong from, int count)
        {
            var quanta = Context.QuantumStorage
                .GetQuanta(from, count);

            if (quanta.Count < 1)
                throw new Exception($"Quanta from {from} apex not found.");

            var results = new List<QuantumSignatures>(quanta.Count);
            foreach (var q in quanta)
            {
                if (q.Signatures == null)
                {
                    logger.Info($"Signatures not found for quantum {q.Quantum.Apex}");
                    break;
                }
                results.Add(new QuantumSignatures { Apex = q.Quantum.Apex, Signatures = q.Signatures });
            }

            return results;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}