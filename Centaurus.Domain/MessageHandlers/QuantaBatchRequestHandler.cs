using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class QuantaBatchRequestHandler : MessageHandlerBase
    {
        public QuantaBatchRequestHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(QuantaBatchRequest).Name;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } =
            new ConnectionState[] {
                ConnectionState.Ready
            };

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var batchRequest = (QuantaBatchRequest)message.Envelope.Message;
            if (connection is IncomingAuditorConnection incomingAuditorConnection)
                incomingAuditorConnection.SetSyncCursor(batchRequest.QuantaCursor, batchRequest.ResultCursor);
            else if (connection is OutgoingConnection)
                await SendQuanta(connection, batchRequest);
            else
                throw new BadRequestException($"Unsupported message type.");
        }

        private async Task SendQuanta(ConnectionBase connection, QuantaBatchRequest batchRequest)
        {
            var aboveApex = batchRequest.QuantaCursor;
            var batchSize = 50;
            while (true)
            {
                if (!Context.QuantumStorage.GetQuantaBacth(aboveApex, batchSize, out var currentBatch)
                    && (aboveApex < Context.QuantumStorage.CurrentApex))
                {
                    currentBatch = Context.DataProvider.GetQuantaAboveApex(aboveApex, batchSize); //quanta are not found in the in-memory storage
                    if (currentBatch.Count < 1)
                        throw new Exception("No quanta from database.");
                }

                if (currentBatch == null)
                    currentBatch = new List<PendingQuantum>();

                var batch = new QuantaBatch
                {
                    Quanta = new List<Message>(),
                    Signatures = new List<QuantumSignatures>(),
                    LastKnownApex = Context.QuantumStorage.CurrentApex
                };

                foreach (var quantum in currentBatch)
                {
                    batch.Quanta.Add(quantum.Quantum);
                    batch.Signatures.Add(new QuantumSignatures { Apex = quantum.Quantum.Apex, Signatures = quantum.Signatures });
                }

                await connection.SendMessage(batch.CreateEnvelope<MessageEnvelopeSignless>());
                var lastQuantum = currentBatch.LastOrDefault();
                aboveApex = lastQuantum?.Quantum.Apex ?? 0;
                if (aboveApex == Context.QuantumStorage.CurrentApex)
                    break;
            };
        }
    }
}