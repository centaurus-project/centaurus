using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorStateRequestHandler : MessageHandlerBase
    {
        public AuditorStateRequestHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorStateRequest;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            var stateRequestMessage = (AuditorStateRequest)message.Envelope.Message;
            var hasQuanta = true;
            var aboveApex = stateRequestMessage.TargetApex;
            var batchSize = 50;
            while (hasQuanta)
            {
                if (!Context.QuantumStorage.GetQuantaBacth(aboveApex + 1, batchSize, out var currentBatch) 
                    && (aboveApex + 1 < Context.QuantumStorage.CurrentApex))
                {
                    currentBatch = await Context.PersistenceManager.GetQuantaAboveApex(aboveApex, batchSize); //quanta are not found in the in-memory storage
                    if (currentBatch.Count < 1)
                        throw new Exception("No quanta from database.");
                }

                if (currentBatch == null)
                    currentBatch = new List<MessageEnvelope>();

                hasQuanta = currentBatch.Count == batchSize;
                var state = new AuditorState
                {
                    State = Context.AppState.State,
                    PendingQuanta = currentBatch,
                    HasMorePendingQuanta = hasQuanta
                };
                await connection.SendMessage(state);
                var lastQuantum = currentBatch.LastOrDefault();
                aboveApex = lastQuantum?.Message.MessageId ?? 0;
            };
        }
    }
}
