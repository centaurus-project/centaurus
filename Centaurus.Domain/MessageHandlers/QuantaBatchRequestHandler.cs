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

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.QuantaBatchRequest;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } =
            new ConnectionState[] {
                ConnectionState.Validated,
                ConnectionState.Ready
            };

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var batchRequest = (QuantaBatchRequest)message.Envelope.Message;
            if (connection is IncomingAuditorConnection incomingAuditorConnection)
                incomingAuditorConnection.SetApexCursor(batchRequest.LastKnownApex);
            else if (connection is OutgoingConnection)
                await SendQuanta(connection, batchRequest);
            else
                throw new BadRequestException($"Unsupported message type.");
        }

        private async Task SendQuanta(ConnectionBase connection, QuantaBatchRequest batchRequest)
        {
            var hasQuanta = true;
            var aboveApex = batchRequest.LastKnownApex;
            var batchSize = 50;
            while (hasQuanta)
            {
                if (!Context.QuantumStorage.GetQuantaBacth(aboveApex + 1, batchSize, out var currentBatch)
                    && (aboveApex + 1 < Context.QuantumStorage.CurrentApex))
                {
                    currentBatch = Context.PersistenceManager.GetQuantaAboveApex(aboveApex, batchSize); //quanta are not found in the in-memory storage
                    if (currentBatch.Count < 1)
                        throw new Exception("No quanta from database.");
                }

                if (currentBatch == null)
                    currentBatch = new List<MessageEnvelope>();

                hasQuanta = currentBatch.Count == batchSize;
                var state = new QuantaBatch
                {
                    Quanta = currentBatch,
                    HasMorePendingQuanta = hasQuanta
                };
                await connection.SendMessage(state);
                var lastQuantum = currentBatch.LastOrDefault();
                aboveApex = (ulong)(lastQuantum?.Message.MessageId ?? 0);
            };
        }
    }
}