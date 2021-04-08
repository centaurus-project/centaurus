using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorStateRequestHandler : BaseAuditorMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorStateRequest;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(AuditorWebSocketConnection connection, IncomingMessage message)
        {
            var stateRequestMessage = (AuditorStateRequest)message.Envelope.Message;
            var hasQuanta = true;
            var aboveApex = stateRequestMessage.TargetApex;
            var quantaPerMessage = 50;
            while (hasQuanta)
            {
                var currentBatch = await connection.Context.PersistenceManager.GetQuantaAboveApex(aboveApex, 50);
                hasQuanta = currentBatch.Count == quantaPerMessage;
                var state = new AuditorState
                {
                    State = connection.Context.AppState.State,
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
