using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AlphaQuantaBatchHandler : MessageHandlerBase<OutgoingConnection>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaQuantaBatchHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override bool IsAuditorOnly => true;

        public override string SupportedMessageType { get; } = typeof(SyncQuantaBatch).Name;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Ready };

        public override async Task HandleMessage(OutgoingConnection connection, IncomingMessage message)
        {
            await AddQuantaToQueue(connection, message);
        }

        private async Task AddQuantaToQueue(OutgoingConnection connection, IncomingMessage message)
        {
            var quantumHandler = Context.QuantumHandler;
            var quantaBatch = (SyncQuantaBatch)message.Envelope.Message;

            //update alpha apex
            Context.StateManager.UpdateAlphaApex(quantaBatch.LastKnownApex);

            //get last known apex
            var lastKnownApex = quantumHandler.LastAddedQuantumApex;

            var quanta = quantaBatch.Quanta;
            foreach (var processedQuantum in quanta)
            {
                var quantum = (Quantum)processedQuantum.Quantum;
                if (quantum.Apex <= lastKnownApex)
                {
                    logger.Trace($"Received {quantum.Apex}, expected {lastKnownApex + 1}");
                    continue;
                }

                if (quantum.Apex != lastKnownApex + 1)
                {
                    await connection.SendMessage(new SyncCursorReset
                    {
                        SyncCursors = new List<SyncCursor> {
                            new SyncCursor {
                                Type = XdrSyncCursorType.Quanta,
                                Cursor = quantumHandler.LastAddedQuantumApex
                            }
                        }
                    }.CreateEnvelope<MessageEnvelopeSignless>());
                    logger.Warn($"Batch has invalid quantum apexes (current: {quantumHandler.LastAddedQuantumApex}, received: {quantum.Apex}). New apex cursor request sent.");
                    return;
                }

                Context.QuantumHandler.HandleAsync(quantum, QuantumSignatureValidator.Validate(quantum));
                Context.ResultManager.Add(new QuantumSignatures
                {
                    Apex = quantum.Apex,
                    Signatures = new List<AuditorSignatureInternal> { processedQuantum.AlphaSignature }
                });
                lastKnownApex = quantum.Apex;
            }
        }
    }
}
