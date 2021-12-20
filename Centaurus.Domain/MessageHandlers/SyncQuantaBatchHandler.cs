using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class SyncQuantaBatchHandler : MessageHandlerBase<OutgoingConnection>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public SyncQuantaBatchHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override bool IsAuditorOnly => true;

        public override string SupportedMessageType { get; } = typeof(SyncQuantaBatch).Name;

        public override bool IsAuthenticatedOnly => true;

        public override async Task HandleMessage(OutgoingConnection connection, IncomingMessage message)
        {
            await AddQuantaToQueue(connection, message);
        }

        private async Task AddQuantaToQueue(OutgoingConnection connection, IncomingMessage message)
        {
            var quantaBatch = (SyncQuantaBatch)message.Envelope.Message;
            var quanta = quantaBatch.Quanta;
            var lastKnownApex = Context.QuantumHandler.LastAddedApex;
            foreach (var processedQuantum in quanta)
            {
                var quantum = (Quantum)processedQuantum.Quantum;
                if (quantum.Apex <= lastKnownApex) //delayed quantum
                    continue;

                if (quantum.Apex != lastKnownApex + 1)
                {
                    await connection.SendMessage(new SyncCursorReset
                    {
                        Cursors = new List<SyncCursor> {
                            new SyncCursor {
                                Type = XdrSyncCursorType.Quanta,
                                Cursor = Context.QuantumHandler.LastAddedApex
                            }
                        }
                    }.CreateEnvelope<MessageEnvelopeSignless>());
                    logger.Warn($"Batch has invalid quantum apexes (current: {Context.QuantumHandler.LastAddedApex}, received: {quantum.Apex}). New apex cursor request sent.");
                    return;
                }

                Context.QuantumHandler.HandleAsync(quantum, QuantumSignatureValidator.Validate(quantum));
                lastKnownApex = quantum.Apex;
            }
        }
    }
}
