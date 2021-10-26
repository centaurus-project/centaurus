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
        public ulong? CurrentSignaturesCursor { get; private set; }

        public void SetCursors(ulong? quantaCursor, ulong? signaturesCursor)
        {
            lock (syncRoot)
            {
                if (quantaCursor != null)
                    CurrentQuantaCursor = quantaCursor;
                if (signaturesCursor != null)
                    CurrentSignaturesCursor = signaturesCursor;
            }
        }


        private (ulong? quantaCursor, ulong? signaturesCursor) GetCursors()
        {
            lock (syncRoot)
                return (CurrentQuantaCursor, CurrentSignaturesCursor);
        }

        private bool IsAuditorReadyToHandleQuanta
        {
            get
            {
                return auditor.ConnectionState == ConnectionState.Ready //connection is validated
                    && (auditor.AuditorState.IsRunning || auditor.AuditorState.IsWaitingForInit); //auditor is ready to handle quanta
            }
        }

        private bool IsCurrentNodeReady => Context.StateManager.State != State.Rising
            && Context.StateManager.State != State.Undefined
            && Context.StateManager.State != State.Failed;

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
                    if (cursors.quantaCursor > Context.QuantumStorage.CurrentApex && IsCurrentNodeReady)
                    {
                        logger.Error($"Auditor {auditor.PubKey.GetAccountId()} is above current constellation state.");
                        await auditor.CloseConnection(System.Net.WebSockets.WebSocketCloseStatus.ProtocolError, "Auditor is above all constellation.");
                        return;
                    }

                    var hasQuantaToSend = ((Context.QuantumStorage.CurrentApex - cursors.quantaCursor) ?? 0) > 0;
                    var hasSignaturesToSend = ((Context.PendingUpdatesManager.LastSavedApex - cursors.signaturesCursor) ?? 0) > 0;

                    if (!Context.IsAlpha //only Alpha should broadcast quanta
                        || !IsAuditorReadyToHandleQuanta //auditor is not ready to handle quanta
                        || (!hasQuantaToSend && !hasSignaturesToSend) //nothing to sync
                        || !IsCurrentNodeReady)
                    {
                        hasPendingQuanta = false;
                        continue;
                    }

                    var quantaSendResult = SendQuanta(hasQuantaToSend, cursors.quantaCursor.Value);

                    var signatureSendResult = SendSignatures(hasSignaturesToSend, cursors.signaturesCursor.Value);

                    await Task.WhenAll(quantaSendResult.sendTask, signatureSendResult.sendTask);

                    lock (syncRoot)
                    {
                        //if quanta cursor is different, than auditor requested new cursor
                        if (quantaSendResult.lastQuantumApex > 0 && CurrentQuantaCursor == cursors.quantaCursor)
                            CurrentQuantaCursor = quantaSendResult.lastQuantumApex;

                        if (signatureSendResult.lastSignaturesApex > 0 && CurrentSignaturesCursor.HasValue && CurrentSignaturesCursor == cursors.signaturesCursor)
                            CurrentSignaturesCursor = signatureSendResult.lastSignaturesApex;
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

        private (Task sendTask, ulong lastQuantumApex) SendQuanta(bool hasQuantaToSend, ulong quantaCursor)
        {
            var lastQuantumApex = 0ul;
            var quantaBatchSendTask = Task.CompletedTask;
            if (hasQuantaToSend && GetPendingQuanta(quantaCursor, batchSize, out var quantaBatch))
            {
                var firstQuantumApex = ((Quantum)quantaBatch.Quanta.First().Qunatum).Apex;
                lastQuantumApex = ((Quantum)quantaBatch.Quanta.Last().Qunatum).Apex;

                logger.Trace($"About to sent {quantaBatch.Quanta.Count} quanta. Range from {firstQuantumApex} to {lastQuantumApex}.");

                quantaBatchSendTask = auditor.SendMessage(quantaBatch.CreateEnvelope<MessageEnvelopeSignless>());
            }
            return (quantaBatchSendTask, lastQuantumApex);
        }

        private (Task sendTask, ulong lastSignaturesApex) SendSignatures(bool hasSignaturesToSend, ulong signaturesCursor)
        {
            var lastSignaturesApex = 0ul;
            var resultBatchSendTask = Task.CompletedTask;
            if (hasSignaturesToSend && GetMajoritySignatures(signaturesCursor, 500, out var signaturesBatch))
            {
                var firstSignaturesApex = signaturesBatch.Signatures.First().Apex;
                lastSignaturesApex = signaturesBatch.Signatures.Last().Apex;
                logger.Trace($"About to sent {signaturesBatch.Signatures.Count} signatures. Range from {firstSignaturesApex} to {lastSignaturesApex}.");

                resultBatchSendTask = auditor.SendMessage(signaturesBatch.CreateEnvelope<MessageEnvelopeSignless>());
            }
            return (resultBatchSendTask, lastSignaturesApex);
        }

        private bool GetPendingQuanta(ulong from, int count, out AlphaQuantaBatch batch)
        {
            var quanta = Context.QuantumStorage
                .GetQuanta(from, count);

            batch = new AlphaQuantaBatch
            {
                Quanta = new List<AlphaQuantaBatchItem>(),
                LastKnownApex = Context.QuantumStorage.CurrentApex
            };
            foreach (var q in quanta)
            {
                if (q.Signatures == null)
                    break;
                batch.Quanta.Add(new AlphaQuantaBatchItem { Qunatum = q.Quantum, AlphaSignature = q.Signatures[0] });
            }
            return batch.Quanta.Count > 0;
        }

        private bool GetMajoritySignatures(ulong from, int count, out Models.QuantumMajoritySignaturesBatch batch)
        {
            var quanta = Context.QuantumStorage
                .GetQuanta(from, count);
            batch = new Models.QuantumMajoritySignaturesBatch
            {
                Signatures = new List<QuantumSignatures>()
            };

            foreach (var q in quanta)
            {
                if (q.Signatures == null || q.Signatures.Count == 1)
                {
                    logger.Trace($"Signatures for quantum {q.Quantum.Apex} not set yet.");
                    break;
                }
                batch.Signatures.Add(new QuantumSignatures { Apex = q.Quantum.Apex, Signatures = q.Signatures });
            }

            return batch.Signatures.Count > 0;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}